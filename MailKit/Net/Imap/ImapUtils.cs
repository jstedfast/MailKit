//
// ImapUtils.cs
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
using System.Diagnostics;
using System.Globalization;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Collections.ObjectModel;

using MimeKit;
using MimeKit.Utils;

namespace MailKit.Net.Imap {
	/// <summary>
	/// IMAP utility functions.
	/// </summary>
	static class ImapUtils
	{
		const FolderAttributes SpecialUseAttributes = FolderAttributes.All | FolderAttributes.Archive | FolderAttributes.Drafts |
		    FolderAttributes.Flagged | FolderAttributes.Important | FolderAttributes.Inbox | FolderAttributes.Junk |
			FolderAttributes.Sent | FolderAttributes.Trash;
		const string QuotedSpecials = " \t()<>@,;:\\\"/[]?=";
		static readonly int InboxLength = "INBOX".Length;

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
			return string.Format (CultureInfo.InvariantCulture, "{0:D2}-{1}-{2:D4} {3:D2}:{4:D2}:{5:D2} {6:+00;-00}{7:00}",
				date.Day, Months[date.Month - 1], date.Year, date.Hour, date.Minute, date.Second,
				date.Offset.Hours, Math.Abs (date.Offset.Minutes));
		}

		static bool TryGetInt32 (string text, ref int index, out int value)
		{
			int startIndex = index;

			value = 0;

			while (index < text.Length && text[index] >= '0' && text[index] <= '9') {
				int digit = text[index] - '0';

				if (value > int.MaxValue / 10 || (value == int.MaxValue / 10 && digit > int.MaxValue % 10)) {
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
			int sign = 1;

			if (text[index] == '-') {
				sign = -1;
				index++;
			} else if (text[index] == '+') {
				index++;
			}

			if (!TryGetInt32 (text, ref index, out var tzone)) {
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

		/// <summary>
		/// Parses the internal date string.
		/// </summary>
		/// <returns>The date.</returns>
		/// <param name="text">The text to parse.</param>
		public static DateTimeOffset ParseInternalDate (string text)
		{
			int index = 0;

			while (index < text.Length && char.IsWhiteSpace (text[index]))
				index++;

			if (index >= text.Length || !TryGetInt32 (text, ref index, '-', out int day) || day < 1 || day > 31)
				return DateTimeOffset.MinValue;

			index++;
			if (index >= text.Length || !TryGetMonth (text, ref index, '-', out int month))
				return DateTimeOffset.MinValue;

			index++;
			if (index >= text.Length || !TryGetInt32 (text, ref index, ' ', out int year) || year < 1969)
				return DateTimeOffset.MinValue;

			index++;
			if (index >= text.Length || !TryGetInt32 (text, ref index, ':', out int hour) || hour > 23)
				return DateTimeOffset.MinValue;

			index++;
			if (index >= text.Length || !TryGetInt32 (text, ref index, ':', out int minute) || minute > 59)
				return DateTimeOffset.MinValue;

			index++;
			if (index >= text.Length || !TryGetInt32 (text, ref index, ' ', out int second) || second > 59)
				return DateTimeOffset.MinValue;

			index++;
			if (index >= text.Length || !TryGetTimeZone (text, ref index, out var timezone))
				return DateTimeOffset.MinValue;

			while (index < text.Length && char.IsWhiteSpace (text[index]))
				index++;

			if (index < text.Length)
				return DateTimeOffset.MinValue;

			// return DateTimeOffset.ParseExact (text.Trim (), "d-MMM-yyyy HH:mm:ss zzz", CultureInfo.InvariantCulture.DateTimeFormat);
			return new DateTimeOffset (year, month, day, hour, minute, second, timezone);
		}

		/// <summary>
		/// Formats a list of annotations for a STORE or APPEND command.
		/// </summary>
		/// <param name="command">The command builder.</param>
		/// <param name="annotations">The annotations.</param>
		/// <param name="args">the argument list.</param>
		/// <param name="throwOnError">Throw an exception if there are any annotations without properties.</param>
		public static void FormatAnnotations (StringBuilder command, IList<Annotation> annotations, List<object> args, bool throwOnError)
		{
			int length = command.Length;
			int added = 0;

			command.Append ("ANNOTATION (");

			for (int i = 0; i < annotations.Count; i++) {
				var annotation = annotations[i];

				if (annotation.Properties.Count == 0) {
					if (throwOnError)
						throw new ArgumentException ("One or more annotations does not define any attributes.", nameof (annotations));

					continue;
				}

				command.Append (annotation.Entry);
				command.Append (" (");

				foreach (var property in annotation.Properties) {
					command.Append (property.Key);
					command.Append (" %S ");
					args.Add (property.Value);
				}

				command[command.Length - 1] = ')';
				command.Append (' ');

				added++;
			}

			if (added > 0)
				command[command.Length - 1] = ')';
			else
				command.Length = length;
		}

		/// <summary>
		/// Formats the array of indexes as a string suitable for use with IMAP commands.
		/// </summary>
		/// <param name="engine">The IMAP engine.</param>
		/// <param name="builder">The string builder.</param>
		/// <param name="indexes">The indexes.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <para><paramref name="engine"/> is <c>null</c>.</para>
		/// <para>-or-</para>
		/// <para><paramref name="builder"/> is <c>null</c>.</para>
		/// <para>-or-</para>
		/// <para><paramref name="indexes"/> is <c>null</c>.</para>
		/// </exception>
		/// <exception cref="System.ArgumentOutOfRangeException">
		/// One or more of the indexes has a negative value.
		/// </exception>
		public static void FormatIndexSet (ImapEngine engine, StringBuilder builder, IList<int> indexes)
		{
			if (engine == null)
				throw new ArgumentNullException (nameof (engine));

			if (builder == null)
				throw new ArgumentNullException (nameof (builder));

			if (indexes == null)
				throw new ArgumentNullException (nameof (indexes));

			if (indexes.Count == 0)
				throw new ArgumentException ("No indexes were specified.", nameof (indexes));

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
					} else if (indexes[i] == end - 1 && engine.QuirksMode != ImapQuirksMode.hMailServer) {
						end = indexes[i++];

						while (i < indexes.Count && indexes[i] == end - 1) {
							end--;
							i++;
						}
					}
				}

				if (index > 0)
					builder.Append (',');

				builder.Append ((begin + 1).ToString (CultureInfo.InvariantCulture));

				if (begin != end) {
					builder.Append (':');
					builder.Append ((end + 1).ToString (CultureInfo.InvariantCulture));
				}

				index = i;
			}
		}

		/// <summary>
		/// Formats the array of indexes as a string suitable for use with IMAP commands.
		/// </summary>
		/// <returns>The index set.</returns>
		/// <param name="engine">The IMAP engine.</param>
		/// <param name="indexes">The indexes.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <para><paramref name="engine"/> is <c>null</c>.</para>
		/// <para>-or-</para>
		/// <para><paramref name="indexes"/> is <c>null</c>.</para>
		/// </exception>
		/// <exception cref="System.ArgumentOutOfRangeException">
		/// One or more of the indexes has a negative value.
		/// </exception>
		public static string FormatIndexSet (ImapEngine engine, IList<int> indexes)
		{
			var builder = new StringBuilder ();

			FormatIndexSet (engine, builder, indexes);

			return builder.ToString ();
		}

		/// <summary>
		/// Parses an untagged ID response.
		/// </summary>
		/// <param name="engine">The IMAP engine.</param>
		/// <param name="ic">The IMAP command.</param>
		static void ParseImplementation (ImapEngine engine, ImapCommand ic)
		{
			var format = string.Format (ImapEngine.GenericUntaggedResponseSyntaxErrorFormat, "ID", "{0}");
			var token = engine.ReadToken (ic.CancellationToken);
			ImapImplementation implementation;

			if (token.Type == ImapTokenType.Nil)
				return;

			ImapEngine.AssertToken (token, ImapTokenType.OpenParen, format, token);

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
		/// Asynchronously parses an untagged ID response.
		/// </summary>
		/// <param name="engine">The IMAP engine.</param>
		/// <param name="ic">The IMAP command.</param>
		static async Task ParseImplementationAsync (ImapEngine engine, ImapCommand ic)
		{
			var format = string.Format (ImapEngine.GenericUntaggedResponseSyntaxErrorFormat, "ID", "{0}");
			var token = await engine.ReadTokenAsync (ic.CancellationToken).ConfigureAwait (false);
			ImapImplementation implementation;

			if (token.Type == ImapTokenType.Nil)
				return;

			ImapEngine.AssertToken (token, ImapTokenType.OpenParen, format, token);

			token = await engine.PeekTokenAsync (ic.CancellationToken).ConfigureAwait (false);

			implementation = new ImapImplementation ();

			while (token.Type != ImapTokenType.CloseParen) {
				var property = await ReadStringTokenAsync (engine, format, ic.CancellationToken).ConfigureAwait (false);
				var value = await ReadNStringTokenAsync (engine, format, false, ic.CancellationToken).ConfigureAwait (false);

				implementation.Properties[property] = value;

				token = await engine.PeekTokenAsync (ic.CancellationToken).ConfigureAwait (false);
			}

			ic.UserData = implementation;

			// read the ')' token
			await engine.ReadTokenAsync (ic.CancellationToken).ConfigureAwait (false);
		}

		/// <summary>
		/// Handles an untagged ID response.
		/// </summary>
		/// <returns>An asynchronous task.</returns>
		/// <param name="engine">The IMAP engine.</param>
		/// <param name="ic">The IMAP command.</param>
		/// <param name="index">The index.</param>
		/// <param name="doAsync">Whether or not asynchronous IO methods should be used.</param>
		public static Task UntaggedIdHandler (ImapEngine engine, ImapCommand ic, int index, bool doAsync)
		{
			if (doAsync)
				return ParseImplementationAsync (engine, ic);

			ParseImplementation (engine, ic);

			return Task.CompletedTask;
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

		static string ReadFolderName (ImapEngine engine, char delim, string format, CancellationToken cancellationToken)
		{
			var token = engine.ReadToken (ImapStream.AtomSpecials, cancellationToken);
			string encodedName;

			switch (token.Type) {
			case ImapTokenType.Literal:
				encodedName = engine.ReadLiteral (cancellationToken);
				break;
			case ImapTokenType.QString:
			case ImapTokenType.Atom:
				encodedName = (string) token.Value;

				// Note: Exchange apparently doesn't quote folder names that contain tabs.
				//
				// See https://github.com/jstedfast/MailKit/issues/945 for details.
				if (engine.QuirksMode == ImapQuirksMode.Exchange) {
					var line = engine.ReadLine (cancellationToken);

					// unget the \r\n sequence
					engine.Stream.UngetToken (ImapToken.Eoln);

					encodedName += line;
				}
				break;
			case ImapTokenType.Nil:
				// Note: according to rfc3501, section 4.5, NIL is acceptable as a mailbox name.
				return "NIL";
			default:
				throw ImapEngine.UnexpectedToken (format, token);
			}

			return encodedName.TrimEnd (delim);
		}

		static async Task<string> ReadFolderNameAsync (ImapEngine engine, char delim, string format, CancellationToken cancellationToken)
		{
			var token = await engine.ReadTokenAsync (ImapStream.AtomSpecials, cancellationToken).ConfigureAwait (false);
			string encodedName;

			switch (token.Type) {
			case ImapTokenType.Literal:
				encodedName = await engine.ReadLiteralAsync (cancellationToken).ConfigureAwait (false);
				break;
			case ImapTokenType.QString:
			case ImapTokenType.Atom:
				encodedName = (string) token.Value;

				// Note: Exchange apparently doesn't quote folder names that contain tabs.
				//
				// See https://github.com/jstedfast/MailKit/issues/945 for details.
				if (engine.QuirksMode == ImapQuirksMode.Exchange) {
					var line = await engine.ReadLineAsync (cancellationToken).ConfigureAwait (false);

					// unget the \r\n sequence
					engine.Stream.UngetToken (ImapToken.Eoln);

					encodedName += line;
				}
				break;
			case ImapTokenType.Nil:
				// Note: according to rfc3501, section 4.5, NIL is acceptable as a mailbox name.
				return "NIL";
			default:
				throw ImapEngine.UnexpectedToken (format, token);
			}

			return encodedName.TrimEnd (delim);
		}

		static void AddFolderAttribute (ref FolderAttributes attrs, string atom)
		{
			if (atom.Equals ("\\noinferiors", StringComparison.OrdinalIgnoreCase))
				attrs |= FolderAttributes.NoInferiors;
			else if (atom.Equals ("\\noselect", StringComparison.OrdinalIgnoreCase))
				attrs |= FolderAttributes.NoSelect;
			else if (atom.Equals ("\\marked", StringComparison.OrdinalIgnoreCase))
				attrs |= FolderAttributes.Marked;
			else if (atom.Equals ("\\unmarked", StringComparison.OrdinalIgnoreCase))
				attrs |= FolderAttributes.Unmarked;
			else if (atom.Equals ("\\nonexistent", StringComparison.OrdinalIgnoreCase))
				attrs |= FolderAttributes.NonExistent;
			else if (atom.Equals ("\\subscribed", StringComparison.OrdinalIgnoreCase))
				attrs |= FolderAttributes.Subscribed;
			else if (atom.Equals ("\\remote", StringComparison.OrdinalIgnoreCase))
				attrs |= FolderAttributes.Remote;
			else if (atom.Equals ("\\haschildren", StringComparison.OrdinalIgnoreCase))
				attrs |= FolderAttributes.HasChildren;
			else if (atom.Equals ("\\hasnochildren", StringComparison.OrdinalIgnoreCase))
				attrs |= FolderAttributes.HasNoChildren;
			else if (atom.Equals ("\\all", StringComparison.OrdinalIgnoreCase))
				attrs |= FolderAttributes.All;
			else if (atom.Equals ("\\archive", StringComparison.OrdinalIgnoreCase))
				attrs |= FolderAttributes.Archive;
			else if (atom.Equals ("\\drafts", StringComparison.OrdinalIgnoreCase))
				attrs |= FolderAttributes.Drafts;
			else if (atom.Equals ("\\flagged", StringComparison.OrdinalIgnoreCase))
				attrs |= FolderAttributes.Flagged;
			else if (atom.Equals ("\\important", StringComparison.OrdinalIgnoreCase))
				attrs |= FolderAttributes.Important;
			else if (atom.Equals ("\\junk", StringComparison.OrdinalIgnoreCase))
				attrs |= FolderAttributes.Junk;
			else if (atom.Equals ("\\sent", StringComparison.OrdinalIgnoreCase))
				attrs |= FolderAttributes.Sent;
			else if (atom.Equals ("\\trash", StringComparison.OrdinalIgnoreCase))
				attrs |= FolderAttributes.Trash;
			// XLIST flags:
			else if (atom.Equals ("\\allmail", StringComparison.OrdinalIgnoreCase))
				attrs |= FolderAttributes.All;
			else if (atom.Equals ("\\inbox", StringComparison.OrdinalIgnoreCase))
				attrs |= FolderAttributes.Inbox;
			else if (atom.Equals ("\\spam", StringComparison.OrdinalIgnoreCase))
				attrs |= FolderAttributes.Junk;
			else if (atom.Equals ("\\starred", StringComparison.OrdinalIgnoreCase))
				attrs |= FolderAttributes.Flagged;
		}

		static void AddFolder (ImapEngine engine, List<ImapFolder> list, ImapFolder folder, string encodedName, char delim, FolderAttributes attrs, bool isLsub, bool returnsSubscribed)
		{
			if (folder != null || engine.TryGetCachedFolder (encodedName, out folder)) {
				if ((attrs & FolderAttributes.NonExistent) != 0) {
					folder.UnsetPermanentFlags ();
					folder.UnsetAcceptedFlags ();
					folder.UpdateUidNext (UniqueId.Invalid);
					folder.UpdateHighestModSeq (0);
					folder.UpdateUidValidity (0);
					folder.UpdateUnread (0);
				}

				if (isLsub) {
					// Note: merge all pre-existing attributes since the LSUB response will not contain them
					attrs |= folder.Attributes | FolderAttributes.Subscribed;
				} else {
					// Note: only merge the SPECIAL-USE and \Subscribed attributes for a LIST command
					attrs |= folder.Attributes & SpecialUseAttributes;

					// Note: only merge \Subscribed if the LIST command isn't expected to include it
					if (!returnsSubscribed)
						attrs |= folder.Attributes & FolderAttributes.Subscribed;
				}

				folder.UpdateAttributes (attrs);
			} else {
				folder = engine.CreateImapFolder (encodedName, attrs, delim);
				engine.CacheFolder (folder);

				if (list == null)
					engine.OnFolderCreated (folder);
			}

			// Note: list will be null if this is an unsolicited LIST response due to an active NOTIFY request
			list?.Add (folder);
		}

		static void ProcessListExtensionProperty (ImapEngine engine, ref ImapFolder folder, string encodedName, char delim, FolderAttributes attrs, string property, string value)
		{
			if (property.Equals ("OLDNAME", StringComparison.OrdinalIgnoreCase)) {
				var oldEncodedName = value.TrimEnd (delim);

				if (engine.FolderCache.TryGetValue (oldEncodedName, out ImapFolder oldFolder)) {
					var args = new ImapFolderConstructorArgs (engine, encodedName, attrs, delim);

					engine.FolderCache.Remove (oldEncodedName);
					engine.FolderCache[encodedName] = oldFolder;
					oldFolder.OnRenamed (args);
					folder = oldFolder;
				}
			}
		}

		static char ParseFolderSeparator (ImapToken token, string format)
		{
			if (token.Type == ImapTokenType.QString) {
				var qstring = (string) token.Value;

				return qstring.Length > 0 ? qstring[0] : '\0';
			} else if (token.Type == ImapTokenType.Nil) {
				return '\0';
			} else {
				throw ImapEngine.UnexpectedToken (format, token);
			}
		}

		/// <summary>
		/// Parses an untagged LIST or LSUB response.
		/// </summary>
		/// <param name="engine">The IMAP engine.</param>
		/// <param name="list">The list of folders to be populated.</param>
		/// <param name="isLsub"><c>true</c> if it is an LSUB response; otherwise, <c>false</c>.</param>
		/// <param name="returnsSubscribed"><c>true</c> if the LIST response is expected to return \Subscribed flags; otherwise, <c>false</c>.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		public static void ParseFolderList (ImapEngine engine, List<ImapFolder> list, bool isLsub, bool returnsSubscribed, CancellationToken cancellationToken)
		{
			var format = string.Format (ImapEngine.GenericUntaggedResponseSyntaxErrorFormat, isLsub ? "LSUB" : "LIST", "{0}");
			var token = engine.ReadToken (cancellationToken);
			var attrs = FolderAttributes.None;
			ImapFolder folder = null;
			string encodedName;
			char delim;

			// parse the folder attributes list
			ImapEngine.AssertToken (token, ImapTokenType.OpenParen, format, token);

			token = engine.ReadToken (cancellationToken);

			while (token.Type == ImapTokenType.Flag || token.Type == ImapTokenType.Atom) {
				var atom = (string) token.Value;

				AddFolderAttribute (ref attrs, atom);

				token = engine.ReadToken (cancellationToken);
			}

			ImapEngine.AssertToken (token, ImapTokenType.CloseParen, format, token);

			// parse the path delimeter
			token = engine.ReadToken (cancellationToken);

			delim = ParseFolderSeparator (token, format);

			encodedName = ReadFolderName (engine, delim, format, cancellationToken);

			if (IsInbox (encodedName))
				attrs |= FolderAttributes.Inbox;

			// peek at the next token to see if we have a LIST extension
			token = engine.PeekToken (cancellationToken);

			if (token.Type == ImapTokenType.OpenParen) {
				// read the '(' token
				engine.ReadToken (cancellationToken);

				do {
					token = engine.ReadToken (cancellationToken);

					if (token.Type == ImapTokenType.CloseParen)
						break;

					// A LIST extension (rfc5258).

					ImapEngine.AssertToken (token, ImapTokenType.Atom, ImapTokenType.QString, format, token);

					var property = (string) token.Value;

					token = engine.ReadToken (cancellationToken);

					ImapEngine.AssertToken (token, ImapTokenType.OpenParen, format, token);

					do {
						token = engine.ReadToken (cancellationToken);

						if (token.Type == ImapTokenType.CloseParen)
							break;

						engine.Stream.UngetToken (token);

						var value = ReadNStringToken (engine, format, false, cancellationToken);

						ProcessListExtensionProperty (engine, ref folder, encodedName, delim, attrs, property, value);
					} while (true);
				} while (true);
			} else {
				ImapEngine.AssertToken (token, ImapTokenType.Eoln, format, token);
			}

			AddFolder (engine, list, folder, encodedName, delim, attrs, isLsub, returnsSubscribed);
		}


		/// <summary>
		/// Asynchronously parses an untagged LIST or LSUB response.
		/// </summary>
		/// <param name="engine">The IMAP engine.</param>
		/// <param name="list">The list of folders to be populated.</param>
		/// <param name="isLsub"><c>true</c> if it is an LSUB response; otherwise, <c>false</c>.</param>
		/// <param name="returnsSubscribed"><c>true</c> if the LIST response is expected to return \Subscribed flags; otherwise, <c>false</c>.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		public static async Task ParseFolderListAsync (ImapEngine engine, List<ImapFolder> list, bool isLsub, bool returnsSubscribed, CancellationToken cancellationToken)
		{
			var format = string.Format (ImapEngine.GenericUntaggedResponseSyntaxErrorFormat, isLsub ? "LSUB" : "LIST", "{0}");
			var token = await engine.ReadTokenAsync (cancellationToken).ConfigureAwait (false);
			var attrs = FolderAttributes.None;
			ImapFolder folder = null;
			string encodedName;
			char delim;

			// parse the folder attributes list
			ImapEngine.AssertToken (token, ImapTokenType.OpenParen, format, token);

			token = await engine.ReadTokenAsync (cancellationToken).ConfigureAwait (false);

			while (token.Type == ImapTokenType.Flag || token.Type == ImapTokenType.Atom) {
				var atom = (string) token.Value;

				AddFolderAttribute (ref attrs, atom);

				token = await engine.ReadTokenAsync (cancellationToken).ConfigureAwait (false);
			}

			ImapEngine.AssertToken (token, ImapTokenType.CloseParen, format, token);

			// parse the path delimeter
			token = await engine.ReadTokenAsync (cancellationToken).ConfigureAwait (false);

			delim = ParseFolderSeparator (token, format);

			encodedName = await ReadFolderNameAsync (engine, delim, format, cancellationToken).ConfigureAwait (false);

			if (IsInbox (encodedName))
				attrs |= FolderAttributes.Inbox;

			// peek at the next token to see if we have a LIST extension
			token = await engine.PeekTokenAsync (cancellationToken).ConfigureAwait (false);

			if (token.Type == ImapTokenType.OpenParen) {
				// read the '(' token
				await engine.ReadTokenAsync (cancellationToken).ConfigureAwait (false);

				do {
					token = await engine.ReadTokenAsync (cancellationToken).ConfigureAwait (false);

					if (token.Type == ImapTokenType.CloseParen)
						break;

					// A LIST extension (rfc5258).

					ImapEngine.AssertToken (token, ImapTokenType.Atom, ImapTokenType.QString, format, token);

					var property = (string) token.Value;

					token = await engine.ReadTokenAsync (cancellationToken).ConfigureAwait (false);

					ImapEngine.AssertToken (token, ImapTokenType.OpenParen, format, token);

					do {
						token = await engine.ReadTokenAsync (cancellationToken).ConfigureAwait (false);

						if (token.Type == ImapTokenType.CloseParen)
							break;

						engine.Stream.UngetToken (token);

						var value = await ReadNStringTokenAsync (engine, format, false, cancellationToken).ConfigureAwait (false);

						ProcessListExtensionProperty (engine, ref folder, encodedName, delim, attrs, property, value);
					} while (true);
				} while (true);
			} else {
				ImapEngine.AssertToken (token, ImapTokenType.Eoln, format, token);
			}

			AddFolder (engine, list, folder, encodedName, delim, attrs, isLsub, returnsSubscribed);
		}

		/// <summary>
		/// Handles an untagged LIST or LSUB response.
		/// </summary>
		/// <param name="engine">The IMAP engine.</param>
		/// <param name="ic">The IMAP command.</param>
		/// <param name="index">The index.</param>
		/// <param name="doAsync">Whether or not asynchronous IO methods should be used.</param>
		public static Task UntaggedListHandler (ImapEngine engine, ImapCommand ic, int index, bool doAsync)
		{
			var list = (List<ImapFolder>) ic.UserData;

			if (doAsync)
				return ParseFolderListAsync (engine, list, ic.Lsub, ic.ListReturnsSubscribed, ic.CancellationToken);

			ParseFolderList (engine, list, ic.Lsub, ic.ListReturnsSubscribed, ic.CancellationToken);

			return Task.CompletedTask;
		}

		/// <summary>
		/// Parses an untagged METADATA response.
		/// </summary>
		/// <returns>The encoded name of the folder that the metadata belongs to.</returns>
		/// <param name="engine">The IMAP engine.</param>
		/// <param name="metadata">The metadata collection to be populated.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		public static void ParseMetadata (ImapEngine engine, MetadataCollection metadata, CancellationToken cancellationToken)
		{
			var format = string.Format (ImapEngine.GenericUntaggedResponseSyntaxErrorFormat, "METADATA", "{0}");
			var encodedName = ReadStringToken (engine, format, cancellationToken);

			var token = engine.ReadToken (cancellationToken);

			ImapEngine.AssertToken (token, ImapTokenType.OpenParen, format, token);

			while (token.Type != ImapTokenType.CloseParen) {
				var tag = ReadStringToken (engine, format, cancellationToken);
				var value = ReadStringToken (engine, format, cancellationToken);

				metadata.Add (new Metadata (MetadataTag.Create (tag), value) { EncodedName = encodedName });

				token = engine.PeekToken (cancellationToken);
			}

			// read the closing paren
			engine.ReadToken (cancellationToken);
		}

		/// <summary>
		/// Asynchronously parses an untagged METADATA response.
		/// </summary>
		/// <returns>The encoded name of the folder that the metadata belongs to.</returns>
		/// <param name="engine">The IMAP engine.</param>
		/// <param name="metadata">The metadata collection to be populated.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		public static async Task ParseMetadataAsync (ImapEngine engine, MetadataCollection metadata, CancellationToken cancellationToken)
		{
			var format = string.Format (ImapEngine.GenericUntaggedResponseSyntaxErrorFormat, "METADATA", "{0}");
			var encodedName = await ReadStringTokenAsync (engine, format, cancellationToken).ConfigureAwait (false);

			var token = await engine.ReadTokenAsync (cancellationToken).ConfigureAwait (false);

			ImapEngine.AssertToken (token, ImapTokenType.OpenParen, format, token);

			while (token.Type != ImapTokenType.CloseParen) {
				var tag = await ReadStringTokenAsync (engine, format, cancellationToken).ConfigureAwait (false);
				var value = await ReadStringTokenAsync (engine, format, cancellationToken).ConfigureAwait (false);

				metadata.Add (new Metadata (MetadataTag.Create (tag), value) { EncodedName = encodedName });

				token = await engine.PeekTokenAsync (cancellationToken).ConfigureAwait (false);
			}

			// read the closing paren
			await engine.ReadTokenAsync (cancellationToken).ConfigureAwait (false);
		}

		/// <summary>
		/// Handles an untagged METADATA response.
		/// </summary>
		/// <param name="engine">The IMAP engine.</param>
		/// <param name="ic">The IMAP command.</param>
		/// <param name="index">The index.</param>
		/// <param name="doAsync">Whether or not asynchronous IO methods should be used.</param>
		public static Task UntaggedMetadataHandler (ImapEngine engine, ImapCommand ic, int index, bool doAsync)
		{
			var metadata = (MetadataCollection) ic.UserData;

			if (doAsync)
				return ParseMetadataAsync (engine, metadata, ic.CancellationToken);

			ParseMetadata (engine, metadata, ic.CancellationToken);

			return Task.CompletedTask;
		}

		internal static string ReadStringToken (ImapEngine engine, string format, CancellationToken cancellationToken)
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

		internal static async ValueTask<string> ReadStringTokenAsync (ImapEngine engine, string format, CancellationToken cancellationToken)
		{
			var token = await engine.ReadTokenAsync (cancellationToken).ConfigureAwait (false);

			switch (token.Type) {
			case ImapTokenType.Literal:
				return await engine.ReadLiteralAsync (cancellationToken).ConfigureAwait (false);
			case ImapTokenType.QString:
			case ImapTokenType.Atom:
				return (string) token.Value;
			default:
				throw ImapEngine.UnexpectedToken (format, token);
			}
		}

		internal static string ReadNStringToken (ImapEngine engine, string format, bool rfc2047, CancellationToken cancellationToken)
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

			if (rfc2047)
				return Rfc2047.DecodeText (TextEncodings.UTF8.GetBytes (value));

			return value;
		}

		internal static async ValueTask<string> ReadNStringTokenAsync (ImapEngine engine, string format, bool rfc2047, CancellationToken cancellationToken)
		{
			var token = await engine.ReadTokenAsync (cancellationToken).ConfigureAwait (false);
			string value;

			switch (token.Type) {
			case ImapTokenType.Literal:
				value = await engine.ReadLiteralAsync (cancellationToken).ConfigureAwait (false);
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

			if (rfc2047)
				return Rfc2047.DecodeText (TextEncodings.UTF8.GetBytes (value));

			return value;
		}

		static uint ParseNumberToken (ImapToken token, string format)
		{
			// Note: this is a work-around for broken IMAP servers that return negative integer values for things
			// like octet counts and line counts.
			if (token.Type == ImapTokenType.Atom) {
				var atom = (string) token.Value;

				if (atom.Length > 0 && atom[0] == '-') {
					if (!int.TryParse (atom, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out _))
						throw ImapEngine.UnexpectedToken (format, token);

					// Note: since Octets & Lines are the only 2 values this method is responsible for parsing,
					// it seems the only sane value to return would be 0.
					return 0;
				}
			}

			return ImapEngine.ParseNumber (token, false, format, token);
		}

		static uint ReadNumber (ImapEngine engine, string format, CancellationToken cancellationToken)
		{
			var token = engine.ReadToken (cancellationToken);

			return ParseNumberToken (token, format);
		}

		static async ValueTask<uint> ReadNumberAsync (ImapEngine engine, string format, CancellationToken cancellationToken)
		{
			var token = await engine.ReadTokenAsync (cancellationToken).ConfigureAwait (false);

			return ParseNumberToken (token, format);
		}

		static bool NeedsQuoting (string value)
		{
			for (int i = 0; i < value.Length; i++) {
				if (value[i] > 127 || char.IsControl (value[i]))
					return true;

				if (QuotedSpecials.IndexOf (value[i]) != -1)
					return true;
			}

			return value.Length == 0;
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
					MimeUtils.AppendQuoted (builder, value);
				else
					builder.Append (value);
			} while (true);

			// read the ')'
			engine.ReadToken (cancellationToken);
		}

		static async Task ParseParameterListAsync (StringBuilder builder, ImapEngine engine, string format, CancellationToken cancellationToken)
		{
			ImapToken token;

			do {
				token = await engine.PeekTokenAsync (cancellationToken).ConfigureAwait (false);

				if (token.Type == ImapTokenType.CloseParen)
					break;

				var name = await ReadStringTokenAsync (engine, format, cancellationToken).ConfigureAwait (false);

				// Note: technically, the value should also be a 'string' token and not an 'nstring',
				// but issue #124 reveals a server that is sending NIL for boundary values.
				var value = await ReadNStringTokenAsync (engine, format, false, cancellationToken).ConfigureAwait (false) ?? string.Empty;

				builder.Append ("; ").Append (name).Append ('=');

				if (NeedsQuoting (value))
					MimeUtils.AppendQuoted (builder, value);
				else
					builder.Append (value);
			} while (true);

			// read the ')'
			await engine.ReadTokenAsync (cancellationToken).ConfigureAwait (false);
		}

		//static readonly string[] MediaTypes = new string[] { "text", "application", "audio", "image", "message", "multipart", "video" };

		static bool IsMediaTypeWithDefaultSubtype (string type, out string subtype)
		{
			if (type.Equals ("text", StringComparison.OrdinalIgnoreCase)) {
				subtype = "plain";
				return true;
			}

			if (type.Equals ("application", StringComparison.OrdinalIgnoreCase)) {
				subtype = "octet-stream";
				return true;
			}

			if (type.Equals ("multipart", StringComparison.OrdinalIgnoreCase)) {
				subtype = "mixed";
				return true;
			}

			// Note: if we ever decide to uncomment this, we'll *probably* have to modify ParseBodyPartAsync() unless
			// we want it to construct a BodyPartMessage. Most likely this will depend on an actual test-case to know
			// what the "correct" behavior should be.
			//if (type.Equals ("message", StringComparison.OrdinalIgnoreCase)) {
			//	subtype = "rfc822";
			//	return true;
			//}

			subtype = null;
			return false;
		}

		static ContentType ParseContentType (ImapEngine engine, string format, CancellationToken cancellationToken)
		{
			var type = ReadNStringToken (engine, format, false, cancellationToken);
			var token = engine.PeekToken (cancellationToken);
			string subtype;

			if (token.Type == ImapTokenType.OpenParen || token.Type == ImapTokenType.Nil) {
				// Note: work around broken IMAP server implementations...
				if (type == null) {
					if (token.Type == ImapTokenType.Nil) {
						// The type and subtype tokens are both NIL. We probably got something like:
						// (NIL NIL NIL NIL NIL "7BIT" 0 NIL NIL NIL NIL)
						// Consume the NIL subtype token.
						engine.ReadToken (cancellationToken);
					}

					type = "application";
					subtype = "octet-stream";
				} else {
					// Note: In some IMAP server implementations, such as the one found in
					// https://github.com/jstedfast/MailKit/issues/371, if the server comes
					// across something like "Content-Type: X-ZIP", it will only send an
					// empty string as the media-type.
					//
					// e.g. ( "X-ZIP" NIL ...) or ( "PLAIN" ("CHARSET" "US-ASCII") ...)
					//
					// Take special note of the leading <SPACE> character after the '('.
					if (!IsMediaTypeWithDefaultSubtype (type, out subtype)) {
						subtype = type;
						type = "application";
					}
				}
			} else {
				subtype = ReadStringToken (engine, format, cancellationToken);
			}

			token = engine.ReadToken (cancellationToken);

			if (token.Type == ImapTokenType.Nil)
				return new ContentType (type, subtype);

			ImapEngine.AssertToken (token, ImapTokenType.OpenParen, format, token);

			var builder = new StringBuilder ();
			builder.Append (type);
			builder.Append ('/');
			builder.Append (subtype);

			ParseParameterList (builder, engine, format, cancellationToken);

			if (!ContentType.TryParse (builder.ToString (), out var contentType))
				contentType = new ContentType (type, subtype);

			return contentType;
		}

		static async Task<ContentType> ParseContentTypeAsync (ImapEngine engine, string format, CancellationToken cancellationToken)
		{
			var type = await ReadNStringTokenAsync (engine, format, false, cancellationToken).ConfigureAwait (false);
			var token = await engine.PeekTokenAsync (cancellationToken).ConfigureAwait (false);
			string subtype;

			if (token.Type == ImapTokenType.OpenParen || token.Type == ImapTokenType.Nil) {
				// Note: work around broken IMAP server implementations...
				if (type == null) {
					if (token.Type == ImapTokenType.Nil) {
						// The type and subtype tokens are both NIL. We probably got something like:
						// (NIL NIL NIL NIL NIL "7BIT" 0 NIL NIL NIL NIL)
						// Consume the NIL subtype token.
						await engine.ReadTokenAsync (cancellationToken).ConfigureAwait (false);
					}

					type = "application";
					subtype = "octet-stream";
				} else {
					// Note: In some IMAP server implementations, such as the one found in
					// https://github.com/jstedfast/MailKit/issues/371, if the server comes
					// across something like "Content-Type: X-ZIP", it will only send an
					// empty string as the media-type.
					//
					// e.g. ( "X-ZIP" NIL ...) or ( "PLAIN" ("CHARSET" "US-ASCII") ...)
					//
					// Take special note of the leading <SPACE> character after the '('.
					if (!IsMediaTypeWithDefaultSubtype (type, out subtype)) {
						subtype = type;
						type = "application";
					}
				}
			} else {
				subtype = await ReadStringTokenAsync (engine, format, cancellationToken).ConfigureAwait (false);
			}

			token = await engine.ReadTokenAsync (cancellationToken).ConfigureAwait (false);

			if (token.Type == ImapTokenType.Nil)
				return new ContentType (type, subtype);

			ImapEngine.AssertToken (token, ImapTokenType.OpenParen, format, token);

			var builder = new StringBuilder ();
			builder.Append (type);
			builder.Append ('/');
			builder.Append (subtype);

			await ParseParameterListAsync (builder, engine, format, cancellationToken).ConfigureAwait (false);

			if (!ContentType.TryParse (builder.ToString (), out var contentType))
				contentType = new ContentType (type, subtype);

			return contentType;
		}

		static ContentDisposition ParseContentDisposition (ImapEngine engine, string format, CancellationToken cancellationToken)
		{
			// body-fld-dsp    = "(" string SP body-fld-param ")" / nil
			var token = engine.ReadToken (cancellationToken);

			if (token.Type == ImapTokenType.Nil)
				return null;

			if (token.Type != ImapTokenType.OpenParen) {
				// Note: this is a work-around for issue #919 where Exchange sends `"inline"` instead of `("inline" NIL)`
				if (token.Type == ImapTokenType.Atom || token.Type == ImapTokenType.QString)
					return new ContentDisposition ((string) token.Value);

				throw ImapEngine.UnexpectedToken (format, token);
			}

			// Exchange bug: ... (NIL NIL) ...
			var dsp = ReadNStringToken (engine, format, false, cancellationToken);
			var builder = new StringBuilder ();
			bool isNil = false;

			// Note: These are work-arounds for some bugs in some mail clients that
			// either leave out the disposition value or quote it.
			//
			// See https://github.com/jstedfast/MailKit/issues/486 for details.
			if (string.IsNullOrEmpty (dsp))
				builder.Append (ContentDisposition.Attachment);
			else
				builder.Append (dsp.Trim ('"'));

			token = engine.ReadToken (cancellationToken);

			if (token.Type == ImapTokenType.OpenParen)
				ParseParameterList (builder, engine, format, cancellationToken);
			else if (token.Type != ImapTokenType.Nil)
				throw ImapEngine.UnexpectedToken (format, token);
			else
				isNil = true;

			token = engine.ReadToken (cancellationToken);

			ImapEngine.AssertToken (token, ImapTokenType.CloseParen, format, token);

			if (dsp == null && isNil)
				return null;

			ContentDisposition.TryParse (builder.ToString (), out var disposition);

			return disposition;
		}

		static async Task<ContentDisposition> ParseContentDispositionAsync (ImapEngine engine, string format, CancellationToken cancellationToken)
		{
			// body-fld-dsp    = "(" string SP body-fld-param ")" / nil
			var token = await engine.ReadTokenAsync (cancellationToken).ConfigureAwait (false);

			if (token.Type == ImapTokenType.Nil)
				return null;

			if (token.Type != ImapTokenType.OpenParen) {
				// Note: this is a work-around for issue #919 where Exchange sends `"inline"` instead of `("inline" NIL)`
				if (token.Type == ImapTokenType.Atom || token.Type == ImapTokenType.QString)
					return new ContentDisposition ((string) token.Value);

				throw ImapEngine.UnexpectedToken (format, token);
			}

			// Exchange bug: ... (NIL NIL) ...
			var dsp = await ReadNStringTokenAsync (engine, format, false, cancellationToken).ConfigureAwait (false);
			var builder = new StringBuilder ();
			bool isNil = false;

			// Note: These are work-arounds for some bugs in some mail clients that
			// either leave out the disposition value or quote it.
			//
			// See https://github.com/jstedfast/MailKit/issues/486 for details.
			if (string.IsNullOrEmpty (dsp))
				builder.Append (ContentDisposition.Attachment);
			else
				builder.Append (dsp.Trim ('"'));

			token = await engine.ReadTokenAsync (cancellationToken).ConfigureAwait (false);

			if (token.Type == ImapTokenType.OpenParen)
				await ParseParameterListAsync (builder, engine, format, cancellationToken).ConfigureAwait (false);
			else if (token.Type != ImapTokenType.Nil)
				throw ImapEngine.UnexpectedToken (format, token);
			else
				isNil = true;

			token = await engine.ReadTokenAsync (cancellationToken).ConfigureAwait (false);

			ImapEngine.AssertToken (token, ImapTokenType.CloseParen, format, token);

			if (dsp == null && isNil)
				return null;

			ContentDisposition.TryParse (builder.ToString (), out var disposition);

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

					// Note: Some broken IMAP servers send `NIL` tokens in this list. Just ignore them.
					//
					// See https://github.com/jstedfast/MailKit/issues/953
					language = ReadNStringToken (engine, format, false, cancellationToken);

					if (language != null)
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

		static async Task<string[]> ParseContentLanguageAsync (ImapEngine engine, string format, CancellationToken cancellationToken)
		{
			var token = await engine.ReadTokenAsync (cancellationToken).ConfigureAwait (false);
			var languages = new List<string> ();
			string language;

			switch (token.Type) {
			case ImapTokenType.Literal:
				language = await engine.ReadLiteralAsync (cancellationToken).ConfigureAwait (false);
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
					token = await engine.PeekTokenAsync (cancellationToken).ConfigureAwait (false);

					if (token.Type == ImapTokenType.CloseParen)
						break;

					// Note: Some broken IMAP servers send `NIL` tokens in this list. Just ignore them.
					//
					// See https://github.com/jstedfast/MailKit/issues/953
					language = await ReadNStringTokenAsync (engine, format, false, cancellationToken).ConfigureAwait (false);

					if (language != null)
						languages.Add (language);
				} while (true);

				// read the ')'
				await engine.ReadTokenAsync (cancellationToken).ConfigureAwait (false);
				break;
			default:
				throw ImapEngine.UnexpectedToken (format, token);
			}

			return languages.ToArray ();
		}

		static Uri ParseContentLocation (string location)
		{
			if (string.IsNullOrWhiteSpace (location))
				return null;

			if (Uri.IsWellFormedUriString (location, UriKind.Absolute))
				return new Uri (location, UriKind.Absolute);

			if (Uri.IsWellFormedUriString (location, UriKind.Relative))
				return new Uri (location, UriKind.Relative);

			return null;
		}

		static Uri ParseContentLocation (ImapEngine engine, string format, CancellationToken cancellationToken)
		{
			var location = ReadNStringToken (engine, format, false, cancellationToken);

			return ParseContentLocation (location);
		}

		static async Task<Uri> ParseContentLocationAsync (ImapEngine engine, string format, CancellationToken cancellationToken)
		{
			var location = await ReadNStringTokenAsync (engine, format, false, cancellationToken).ConfigureAwait (false);

			return ParseContentLocation (location);
		}

		static void SkipBodyExtension (ImapEngine engine, string format, CancellationToken cancellationToken)
		{
			var token = engine.ReadToken (cancellationToken);

			switch (token.Type) {
			case ImapTokenType.OpenParen:
				do {
					token = engine.PeekToken (cancellationToken);

					if (token.Type == ImapTokenType.CloseParen)
						break;

					SkipBodyExtension (engine, format, cancellationToken);
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

		static async Task SkipBodyExtensionAsync (ImapEngine engine, string format, CancellationToken cancellationToken)
		{
			var token = await engine.ReadTokenAsync (cancellationToken).ConfigureAwait (false);

			switch (token.Type) {
			case ImapTokenType.OpenParen:
				do {
					token = await engine.PeekTokenAsync (cancellationToken).ConfigureAwait (false);

					if (token.Type == ImapTokenType.CloseParen)
						break;

					await SkipBodyExtensionAsync (engine, format, cancellationToken).ConfigureAwait (false);
				} while (true);

				// read the ')'
				await engine.ReadTokenAsync (cancellationToken).ConfigureAwait (false);
				break;
			case ImapTokenType.Literal:
				await engine.ReadLiteralAsync (cancellationToken).ConfigureAwait (false);
				break;
			case ImapTokenType.QString:
			case ImapTokenType.Atom:
			case ImapTokenType.Nil:
				break;
			default:
				throw ImapEngine.UnexpectedToken (format, token);
			}
		}

		static BodyPart ParseMultipart (ImapEngine engine, string format, string path, CancellationToken cancellationToken)
		{
			var prefix = path.Length > 0 ? path + "." : string.Empty;
			var body = new BodyPartMultipart ();
			ImapToken token;
			int index = 1;

			token = engine.PeekToken (cancellationToken);

			if (token.Type != ImapTokenType.Nil) {
				do {
					body.BodyParts.Add (ParseBody (engine, format, prefix + index, cancellationToken));
					token = engine.PeekToken (cancellationToken);
					index++;
				} while (token.Type == ImapTokenType.OpenParen);
			} else {
				// Note: Sometimes, when a multipart contains no children, IMAP servers (even Dovecot!)
				// will reply with a BODYSTRUCTURE that looks like (NIL "alternative" ("boundary" "...
				// Obviously, this is not a body-type-1part because "alternative" is a multipart subtype.
				// This suggests that the NIL represents an empty list of children.
				//
				// See https://github.com/jstedfast/MailKit/issues/1393 for more details.
				engine.ReadToken (cancellationToken);
			}

			var subtype = ReadStringToken (engine, format, cancellationToken);

			body.ContentType = new ContentType ("multipart", subtype);
			body.PartSpecifier = path;

			token = engine.PeekToken (cancellationToken);

			if (token.Type != ImapTokenType.CloseParen) {
				token = engine.ReadToken (cancellationToken);

				ImapEngine.AssertToken (token, ImapTokenType.OpenParen, ImapTokenType.Nil, format, token);

				var builder = new StringBuilder ();
				builder.Append (body.ContentType.MediaType);
				builder.Append ('/');
				builder.Append (body.ContentType.MediaSubtype);

				if (token.Type == ImapTokenType.OpenParen)
					ParseParameterList (builder, engine, format, cancellationToken);

				if (ContentType.TryParse (builder.ToString (), out var contentType))
					body.ContentType = contentType;

				token = engine.PeekToken (cancellationToken);
			}

			if (token.Type == ImapTokenType.QString) {
				// Note: This is a work-around for broken Exchange servers.
				//
				// See https://stackoverflow.com/questions/33481604/mailkit-fetch-unexpected-token-in-imap-response-qstring-multipart-message
				// for details.

				// Read what appears to be a Content-Description.
				token = engine.ReadToken (cancellationToken);

				// Peek ahead at the next token. It has been suggested that this next token seems to be the Content-Language value.
				token = engine.PeekToken (cancellationToken);
			} else if (token.Type != ImapTokenType.CloseParen) {
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

			while (token.Type != ImapTokenType.CloseParen) {
				SkipBodyExtension (engine, format, cancellationToken);
				token = engine.PeekToken (cancellationToken);
			}

			// read the ')'
			engine.ReadToken (cancellationToken);

			return body;
		}

		static async Task<BodyPart> ParseMultipartAsync (ImapEngine engine, string format, string path, CancellationToken cancellationToken)
		{
			var prefix = path.Length > 0 ? path + "." : string.Empty;
			var body = new BodyPartMultipart ();
			ImapToken token;
			int index = 1;

			token = await engine.PeekTokenAsync (cancellationToken).ConfigureAwait (false);

			if (token.Type != ImapTokenType.Nil) {
				do {
					body.BodyParts.Add (await ParseBodyAsync (engine, format, prefix + index, cancellationToken).ConfigureAwait (false));
					token = await engine.PeekTokenAsync (cancellationToken).ConfigureAwait (false);
					index++;
				} while (token.Type == ImapTokenType.OpenParen);
			} else {
				// Note: Sometimes, when a multipart contains no children, IMAP servers (even Dovecot!)
				// will reply with a BODYSTRUCTURE that looks like (NIL "alternative" ("boundary" "...
				// Obviously, this is not a body-type-1part because "alternative" is a multipart subtype.
				// This suggests that the NIL represents an empty list of children.
				//
				// See https://github.com/jstedfast/MailKit/issues/1393 for more details.
				await engine.ReadTokenAsync (cancellationToken).ConfigureAwait (false);
			}

			var subtype = await ReadStringTokenAsync (engine, format, cancellationToken).ConfigureAwait (false);

			body.ContentType = new ContentType ("multipart", subtype);
			body.PartSpecifier = path;

			token = await engine.PeekTokenAsync (cancellationToken).ConfigureAwait (false);

			if (token.Type != ImapTokenType.CloseParen) {
				token = await engine.ReadTokenAsync (cancellationToken).ConfigureAwait (false);

				ImapEngine.AssertToken (token, ImapTokenType.OpenParen, ImapTokenType.Nil, format, token);

				var builder = new StringBuilder ();
				builder.Append (body.ContentType.MediaType);
				builder.Append ('/');
				builder.Append (body.ContentType.MediaSubtype);

				if (token.Type == ImapTokenType.OpenParen)
					await ParseParameterListAsync (builder, engine, format, cancellationToken).ConfigureAwait (false);

				if (ContentType.TryParse (builder.ToString (), out var contentType))
					body.ContentType = contentType;

				token = await engine.PeekTokenAsync (cancellationToken).ConfigureAwait (false);
			}

			if (token.Type == ImapTokenType.QString) {
				// Note: This is a work-around for broken Exchange servers.
				//
				// See https://stackoverflow.com/questions/33481604/mailkit-fetch-unexpected-token-in-imap-response-qstring-multipart-message
				// for details.

				// Read what appears to be a Content-Description.
				token = await engine.ReadTokenAsync (cancellationToken).ConfigureAwait (false);

				// Peek ahead at the next token. It has been suggested that this next token seems to be the Content-Language value.
				token = await engine.PeekTokenAsync (cancellationToken).ConfigureAwait (false);
			} else if (token.Type != ImapTokenType.CloseParen) {
				body.ContentDisposition = await ParseContentDispositionAsync (engine, format, cancellationToken).ConfigureAwait (false);
				token = await engine.PeekTokenAsync (cancellationToken).ConfigureAwait (false);
			}

			if (token.Type != ImapTokenType.CloseParen) {
				body.ContentLanguage = await ParseContentLanguageAsync (engine, format, cancellationToken).ConfigureAwait (false);
				token = await engine.PeekTokenAsync (cancellationToken).ConfigureAwait (false);
			}

			if (token.Type != ImapTokenType.CloseParen) {
				body.ContentLocation = await ParseContentLocationAsync (engine, format, cancellationToken).ConfigureAwait (false);
				token = await engine.PeekTokenAsync (cancellationToken).ConfigureAwait (false);
			}

			while (token.Type != ImapTokenType.CloseParen) {
				await SkipBodyExtensionAsync (engine, format, cancellationToken).ConfigureAwait (false);
				token = await engine.PeekTokenAsync (cancellationToken).ConfigureAwait (false);
			}

			// read the ')'
			await engine.ReadTokenAsync (cancellationToken).ConfigureAwait (false);

			return body;
		}

		static bool ShouldParseMultipart (ImapEngine engine, CancellationToken cancellationToken)
		{
			var token = engine.ReadToken (cancellationToken);
			ImapToken nextToken;

			switch (token.Type) {
			case ImapTokenType.Atom: // Note: Technically, we should never get an Atom here, but if we do, we'll treat it as a QString.
			case ImapTokenType.QString:
			case ImapTokenType.Literal:
				if (engine.QuirksMode == ImapQuirksMode.GMail && token.Type != ImapTokenType.Literal) {
					// Note: GMail's IMAP server implementation breaks when it encounters nested multiparts with the same
					// boundary and returns a BODYSTRUCTURE like the example in https://github.com/jstedfast/MailKit/issues/205
					// or like the example in https://github.com/jstedfast/MailKit/issues/777:
					//
					// ("ALTERNATIVE" ("BOUNDARY" "==alternative_xad5934455aeex") NIL NIL)
					// or
					// ("RELATED" NIL ("ATTACHMENT" NIL) NIL)
					//
					// Check if the next token is either a '(' or NIL. If it is '(', then that would indicate the start of
					// the Content-Type parameter list. If it is NIL, then it would signify that the Content-Type has no
					// parameters.

					// Peek at the next token to see what we've got. If we get a '(' or NIL, then treat this as a multipart.
					nextToken = engine.PeekToken (cancellationToken);

					if (nextToken.Type == ImapTokenType.OpenParen || nextToken.Type == ImapTokenType.Nil) {
						// Unget the multipart subtype.
						engine.Stream.UngetToken (token);

						// Now unget a fake NIL token that represents an empty set of children.
						engine.Stream.UngetToken (ImapToken.Nil);

						return true;
					}

					// Fall through and treat things nomrally.
				}

				// We've got a string which normally means it's the first token of a mime-type.
				engine.Stream.UngetToken (token);
				return false;
			case ImapTokenType.OpenParen:
				// We've got children, so this is definitely a multipart.
				engine.Stream.UngetToken (token);
				return true;
			case ImapTokenType.Nil:
				// We've got a NIL token. Technically, this is illegal syntax, but we need to be able to handle it.
				//
				// There are currently 2 known examples of this:
				//
				// 1. Sometimes, when a multipart contains no children, IMAP servers (even Dovecot!)
				// will reply with a BODYSTRUCTURE that looks like (NIL "alternative" ("boundary" "...
				// Obviously, this is not a body-type-1part because "alternative" is a multipart subtype.
				// This suggests that the NIL represents an empty list of children.
				//
				// For an example of this particular case, see https://github.com/jstedfast/MailKit/issues/1393.
				//
				// 2. There have been several reports of Office365 sending body-type-1parts of the following form:
				// (NIL NIL NIL NIL NIL "7BIT" 0 NIL NIL NIL NIL)
				//
				// Presumably this is a text/plain part with no headers?
				//
				// For examples of this, see:
				// https://github.com/jstedfast/MailKit/issues/1415#issuecomment-1206533214 and
				// https://github.com/jstedfast/MailKit/issues/1446
				nextToken = engine.PeekToken (cancellationToken);

				engine.Stream.UngetToken (token);

				if (nextToken.Type == ImapTokenType.Nil) {
					// Looks like we've probably encountered the `(NIL NIL NIL NIL NIL "7BIT" 0 NIL NIL NIL NIL)` variant.
					return false;
				}

				// Assume (NIL "alternative" ("boundary" "...
				return true;
			default:
				engine.Stream.UngetToken (token);
				return false;
			}
		}

		static async Task<bool> ShouldParseMultipartAsync (ImapEngine engine, CancellationToken cancellationToken)
		{
			var token = await engine.ReadTokenAsync (cancellationToken).ConfigureAwait (false);
			ImapToken nextToken;

			switch (token.Type) {
			case ImapTokenType.Atom: // Note: Technically, we should never get an Atom here, but if we do, we'll treat it as a QString.
			case ImapTokenType.QString:
			case ImapTokenType.Literal:
				if (engine.QuirksMode == ImapQuirksMode.GMail && token.Type != ImapTokenType.Literal) {
					// Note: GMail's IMAP server implementation breaks when it encounters nested multiparts with the same
					// boundary and returns a BODYSTRUCTURE like the example in https://github.com/jstedfast/MailKit/issues/205
					// or like the example in https://github.com/jstedfast/MailKit/issues/777:
					//
					// ("ALTERNATIVE" ("BOUNDARY" "==alternative_xad5934455aeex") NIL NIL)
					// or
					// ("RELATED" NIL ("ATTACHMENT" NIL) NIL)
					//
					// Check if the next token is either a '(' or NIL. If it is '(', then that would indicate the start of
					// the Content-Type parameter list. If it is NIL, then it would signify that the Content-Type has no
					// parameters.

					// Peek at the next token to see what we've got. If we get a '(' or NIL, then treat this as a multipart.
					nextToken = await engine.PeekTokenAsync (cancellationToken).ConfigureAwait (false);

					if (nextToken.Type == ImapTokenType.OpenParen || nextToken.Type == ImapTokenType.Nil) {
						// Unget the multipart subtype.
						engine.Stream.UngetToken (token);

						// Now unget a fake NIL token that represents an empty set of children.
						engine.Stream.UngetToken (ImapToken.Nil);

						return true;
					}

					// Fall through and treat things nomrally.
				}

				// We've got a string which normally means it's the first token of a mime-type.
				engine.Stream.UngetToken (token);
				return false;
			case ImapTokenType.OpenParen:
				// We've got children, so this is definitely a multipart.
				engine.Stream.UngetToken (token);
				return true;
			case ImapTokenType.Nil:
				// We've got a NIL token. Technically, this is illegal syntax, but we need to be able to handle it.
				//
				// There are currently 2 known examples of this:
				//
				// 1. Sometimes, when a multipart contains no children, IMAP servers (even Dovecot!)
				// will reply with a BODYSTRUCTURE that looks like (NIL "alternative" ("boundary" "...
				// Obviously, this is not a body-type-1part because "alternative" is a multipart subtype.
				// This suggests that the NIL represents an empty list of children.
				//
				// For an example of this particular case, see https://github.com/jstedfast/MailKit/issues/1393.
				//
				// 2. There have been several reports of Office365 sending body-type-1parts of the following form:
				// (NIL NIL NIL NIL NIL "7BIT" 0 NIL NIL NIL NIL)
				//
				// Presumably this is a text/plain part with no headers?
				//
				// For examples of this, see:
				// https://github.com/jstedfast/MailKit/issues/1415#issuecomment-1206533214 and
				// https://github.com/jstedfast/MailKit/issues/1446
				nextToken = await engine.PeekTokenAsync (cancellationToken).ConfigureAwait (false);

				engine.Stream.UngetToken (token);

				if (nextToken.Type == ImapTokenType.Nil) {
					// Looks like we've probably encountered the `(NIL NIL NIL NIL NIL "7BIT" 0 NIL NIL NIL NIL)` variant.
					return false;
				}

				// Assume (NIL "alternative" ("boundary" "...
				return true;
			default:
				engine.Stream.UngetToken (token);
				return false;
			}
		}

		public static BodyPart ParseBody (ImapEngine engine, string format, string path, CancellationToken cancellationToken)
		{
			var token = engine.ReadToken (cancellationToken);

			if (token.Type == ImapTokenType.Nil)
				return null;

			ImapEngine.AssertToken (token, ImapTokenType.OpenParen, format, token);

			token = engine.PeekToken (cancellationToken);

			// Note: If we immediately get a closing ')', then treat it the same as if we had gotten a `NIL` `body` token.
			//
			// See https://github.com/jstedfast/MailKit/issues/944 for details.
			if (token.Type == ImapTokenType.CloseParen) {
				engine.ReadToken (cancellationToken);
				return null;
			}

			if (ShouldParseMultipart (engine, cancellationToken))
				return ParseMultipart (engine, format, path, cancellationToken);

			var type = ParseContentType (engine, format, cancellationToken);
			var id = ReadNStringToken (engine, format, false, cancellationToken);
			var desc = ReadNStringToken (engine, format, true, cancellationToken);
			// Note: technically, body-fld-enc, is not allowed to be NIL, but we need to deal with broken servers...
			var enc = ReadNStringToken (engine, format, false, cancellationToken);
			var octets = ReadNumber (engine, format, cancellationToken);
			var isMultipart = false;
			BodyPartBasic body;

			if (type.IsMimeType ("message", "rfc822")) {
				var rfc822 = new BodyPartMessage ();

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
					rfc822.Envelope = ParseEnvelope (engine, cancellationToken);
					rfc822.Body = ParseBody (engine, format, path, cancellationToken);
					rfc822.Lines = ReadNumber (engine, format, cancellationToken);
				}

				body = rfc822;
			} else if (type.IsMimeType ("text", "*")) {
				var text = new BodyPartText {
					Lines = ReadNumber (engine, format, cancellationToken)
				};
				body = text;
			} else {
				isMultipart = type.IsMimeType ("multipart", "*");
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

			if (!isMultipart) {
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

				if (token.Type != ImapTokenType.CloseParen && token.Type != ImapTokenType.OpenParen) {
					body.ContentLocation = ParseContentLocation (engine, format, cancellationToken);
					token = engine.PeekToken (cancellationToken);
				}
			}

			while (token.Type != ImapTokenType.CloseParen) {
				SkipBodyExtension (engine, format, cancellationToken);
				token = engine.PeekToken (cancellationToken);
			}

			// read the ')'
			engine.ReadToken (cancellationToken);

			return body;
		}

		public static async Task<BodyPart> ParseBodyAsync (ImapEngine engine, string format, string path, CancellationToken cancellationToken)
		{
			var token = await engine.ReadTokenAsync (cancellationToken).ConfigureAwait (false);

			if (token.Type == ImapTokenType.Nil)
				return null;

			ImapEngine.AssertToken (token, ImapTokenType.OpenParen, format, token);

			token = await engine.PeekTokenAsync (cancellationToken).ConfigureAwait (false);

			// Note: If we immediately get a closing ')', then treat it the same as if we had gotten a `NIL` `body` token.
			//
			// See https://github.com/jstedfast/MailKit/issues/944 for details.
			if (token.Type == ImapTokenType.CloseParen) {
				await engine.ReadTokenAsync (cancellationToken).ConfigureAwait (false);
				return null;
			}

			if (await ShouldParseMultipartAsync (engine, cancellationToken).ConfigureAwait (false))
				return await ParseMultipartAsync (engine, format, path, cancellationToken).ConfigureAwait (false);

			var type = await ParseContentTypeAsync (engine, format, cancellationToken).ConfigureAwait (false);
			var id = await ReadNStringTokenAsync (engine, format, false, cancellationToken).ConfigureAwait (false);
			var desc = await ReadNStringTokenAsync (engine, format, true, cancellationToken).ConfigureAwait (false);
			// Note: technically, body-fld-enc, is not allowed to be NIL, but we need to deal with broken servers...
			var enc = await ReadNStringTokenAsync (engine, format, false, cancellationToken).ConfigureAwait (false);
			var octets = await ReadNumberAsync (engine, format, cancellationToken).ConfigureAwait (false);
			var isMultipart = false;
			BodyPartBasic body;

			if (type.IsMimeType ("message", "rfc822")) {
				var rfc822 = new BodyPartMessage ();

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
				token = await engine.PeekTokenAsync (cancellationToken).ConfigureAwait (false);

				if (token.Type == ImapTokenType.OpenParen) {
					rfc822.Envelope = await ParseEnvelopeAsync (engine, cancellationToken).ConfigureAwait (false);
					rfc822.Body = await ParseBodyAsync (engine, format, path, cancellationToken).ConfigureAwait (false);
					rfc822.Lines = await ReadNumberAsync (engine, format, cancellationToken).ConfigureAwait (false);
				}

				body = rfc822;
			} else if (type.IsMimeType ("text", "*")) {
				var text = new BodyPartText {
					Lines = await ReadNumberAsync (engine, format, cancellationToken).ConfigureAwait (false)
				};
				body = text;
			} else {
				isMultipart = type.IsMimeType ("multipart", "*");
				body = new BodyPartBasic ();
			}

			body.ContentTransferEncoding = enc;
			body.ContentDescription = desc;
			body.PartSpecifier = path;
			body.ContentType = type;
			body.ContentId = id;
			body.Octets = octets;

			// if we are parsing a BODYSTRUCTURE, we may get some more tokens before the ')'
			token = await engine.PeekTokenAsync (cancellationToken).ConfigureAwait (false);

			if (!isMultipart) {
				if (token.Type != ImapTokenType.CloseParen) {
					body.ContentMd5 = await ReadNStringTokenAsync (engine, format, false, cancellationToken).ConfigureAwait (false);
					token = await engine.PeekTokenAsync (cancellationToken).ConfigureAwait (false);
				}

				if (token.Type != ImapTokenType.CloseParen) {
					body.ContentDisposition = await ParseContentDispositionAsync (engine, format, cancellationToken).ConfigureAwait (false);
					token = await engine.PeekTokenAsync (cancellationToken).ConfigureAwait (false);
				}

				if (token.Type != ImapTokenType.CloseParen) {
					body.ContentLanguage = await ParseContentLanguageAsync (engine, format, cancellationToken).ConfigureAwait (false);
					token = await engine.PeekTokenAsync (cancellationToken).ConfigureAwait (false);
				}

				if (token.Type != ImapTokenType.CloseParen && token.Type != ImapTokenType.OpenParen) {
					body.ContentLocation = await ParseContentLocationAsync (engine, format, cancellationToken).ConfigureAwait (false);
					token = await engine.PeekTokenAsync (cancellationToken).ConfigureAwait (false);
				}
			}

			while (token.Type != ImapTokenType.CloseParen) {
				await SkipBodyExtensionAsync (engine, format, cancellationToken).ConfigureAwait (false);
				token = await engine.PeekTokenAsync (cancellationToken).ConfigureAwait (false);
			}

			// read the ')'
			await engine.ReadTokenAsync (cancellationToken).ConfigureAwait (false);

			return body;
		}

		readonly struct EnvelopeAddress
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
				get { return Name == null && Route == null && Mailbox != null && Domain == null; }
			}

			public bool IsGroupEnd {
				get { return Name == null && Route == null && Mailbox == null && Domain == null; }
			}

			public MailboxAddress ToMailboxAddress (ImapEngine engine)
			{
				if (engine.QuirksMode == ImapQuirksMode.GMail && Name != null && Name[0] == '<' && Name[Name.Length - 1] == '>' && Mailbox != null && Domain == null) {
					// For whatever reason, GMail seems to sometimes break by reversing the Name and Mailbox tokens.
					// For an example, see the second error report in https://github.com/jstedfast/MailKit/issues/494
					// where the Sender: address in the ENVELOPE has the name and address tokens flipped.
					//
					// Another example can be seen in https://github.com/jstedfast/MailKit/pull/1319.
					var reversed = string.Format ("{0} {1}", Mailbox, Name);

					try {
						return MailboxAddress.Parse (reversed);
					} catch (ParseException) {
						// fall through to normal processing
					}
				}

				var mailbox = Mailbox;
				var domain = Domain;
				string name = null;
				string address;

				if (Name != null)
					name = Rfc2047.DecodePhrase (TextEncodings.UTF8.GetBytes (Name));

				// Note: When parsing mailbox addresses w/o a domain, Dovecot will
				// use "MISSING_DOMAIN" as the domain string to prevent it from
				// appearing as a group address in the IMAP ENVELOPE response.
				if (domain == "MISSING_DOMAIN" || domain == ".MISSING-HOST-NAME.")
					domain = null;
				else if (domain != null)
					domain = domain.TrimEnd ('>');

				if (mailbox != null) {
					mailbox = mailbox.TrimStart ('<');

					address = domain != null ? mailbox + "@" + domain : mailbox;
				} else {
					address = string.Empty;
				}

				if (Route != null && DomainList.TryParse (Route, out var route))
					return new MailboxAddress (name, route, address);

				return new MailboxAddress (name, address);
			}

			public GroupAddress ToGroupAddress (ImapEngine engine)
			{
				var name = string.Empty;

				if (Mailbox != null)
					name = Rfc2047.DecodePhrase (TextEncodings.UTF8.GetBytes (Mailbox));

				return new GroupAddress (name);
			}
		}

		static bool TryAddEnvelopeAddressToken (ImapToken token, ref int index, string[] values, bool[] qstrings, string format)
		{
			// This is a work-around for mail servers which output too many tokens for an ENVELOPE address. In at least 1 case, this happened
			// because the server sent a literal token as the name component and miscalculated the literal length as 38 when it was actually 69
			// (likely using Unicode characters instead of UTF-8 bytes).
			//
			// The work-around is to keep merging tokens at the beginning of the list until we end up with only 4 tokens.
			//
			// See https://github.com/jstedfast/MailKit/issues/1369 for details.
			if (index >= 4) {
				if (qstrings[0])
					values[0] = MimeUtils.Quote (values[0]);
				if (qstrings[1])
					values[1] = MimeUtils.Quote (values[1]);
				values[0] = values[0] + ' ' + values[1];
				qstrings[0] = false;
				qstrings[1] = qstrings[2];
				values[1] = values[2];
				qstrings[2] = qstrings[3];
				values[2] = values[3];
				index = 3;
			}

			switch (token.Type) {
			case ImapTokenType.Literal:
				// Return control to our caller so that it can read the literal token.
				qstrings[index] = false;
				return false;
			case ImapTokenType.QString:
				values[index] = (string) token.Value;
				qstrings[index] = true;
				break;
			case ImapTokenType.Atom:
				values[index] = (string) token.Value;
				qstrings[index] = false;
				break;
			case ImapTokenType.Nil:
				values[index] = null;
				qstrings[index] = false;
				break;
			default:
				throw ImapEngine.UnexpectedToken (format, token);
			}

			return true;
		}

		static EnvelopeAddress ParseEnvelopeAddress (ImapEngine engine, string format, CancellationToken cancellationToken)
		{
			var values = new string[4];
			var qstrings = new bool[4];
			ImapToken token;
			int index = 0;

			do {
				token = engine.ReadToken (cancellationToken);

				if (token.Type == ImapTokenType.CloseParen)
					break;

				if (!TryAddEnvelopeAddressToken (token, ref index, values, qstrings, format))
					values[index] = engine.ReadLiteral (cancellationToken);

				index++;
			} while (true);

			ImapEngine.AssertToken (token, ImapTokenType.CloseParen, format, token);

			return new EnvelopeAddress (values);
		}

		static async Task<EnvelopeAddress> ParseEnvelopeAddressAsync (ImapEngine engine, string format, CancellationToken cancellationToken)
		{
			var values = new string[4];
			var qstrings = new bool[4];
			ImapToken token;
			int index = 0;

			do {
				token = await engine.ReadTokenAsync (cancellationToken).ConfigureAwait (false);

				if (token.Type == ImapTokenType.CloseParen)
					break;

				if (!TryAddEnvelopeAddressToken (token, ref index, values, qstrings, format))
					values[index] = await engine.ReadLiteralAsync (cancellationToken).ConfigureAwait (false);

				index++;
			} while (true);

			ImapEngine.AssertToken (token, ImapTokenType.CloseParen, format, token);

			return new EnvelopeAddress (values);
		}

		static void AddEnvelopeAddress (ImapEngine engine, List<InternetAddressList> stack, ref int sp, EnvelopeAddress address)
		{
			if (address.IsGroupStart && engine.QuirksMode != ImapQuirksMode.GMail) {
				var group = address.ToGroupAddress (engine);
				stack[sp].Add (group);
				stack.Add (group.Members);
				sp++;
			} else if (address.IsGroupEnd) {
				if (sp > 0) {
					stack.RemoveAt (sp);
					sp--;
				}
			} else {
				try {
					// Note: We need to do a try/catch around ToMailboxAddress() because some addresses
					// returned by the IMAP server might be completely horked. For an example, see the
					// second error report in https://github.com/jstedfast/MailKit/issues/494 where one
					// of the addresses in the ENVELOPE has the name and address tokens flipped.
					var mailbox = address.ToMailboxAddress (engine);
					stack[sp].Add (mailbox);
				} catch {
					return;
				}
			}
		}

		static void ParseEnvelopeAddressList (InternetAddressList list, ImapEngine engine, string format, CancellationToken cancellationToken)
		{
			var token = engine.ReadToken (cancellationToken);

			if (token.Type == ImapTokenType.Nil)
				return;

			ImapEngine.AssertToken (token, ImapTokenType.OpenParen, format, token);

			var stack = new List<InternetAddressList> ();
			int sp = 0;

			stack.Add (list);

			do {
				token = engine.ReadToken (cancellationToken);

				if (token.Type == ImapTokenType.CloseParen)
					break;

				// Note: As seen in https://github.com/jstedfast/MailKit/issues/991, it seems that SmarterMail IMAP
				// servers will sometimes include a NIL address token within the address list. Just ignore it.
				if (token.Type == ImapTokenType.Nil)
					continue;

				ImapEngine.AssertToken (token, ImapTokenType.OpenParen, format, token);

				var address = ParseEnvelopeAddress (engine, format, cancellationToken);

				AddEnvelopeAddress (engine, stack, ref sp, address);
			} while (true);
		}

		static async Task ParseEnvelopeAddressListAsync (InternetAddressList list, ImapEngine engine, string format, CancellationToken cancellationToken)
		{
			var token = await engine.ReadTokenAsync (cancellationToken).ConfigureAwait (false);

			if (token.Type == ImapTokenType.Nil)
				return;

			ImapEngine.AssertToken (token, ImapTokenType.OpenParen, format, token);

			var stack = new List<InternetAddressList> ();
			int sp = 0;

			stack.Add (list);

			do {
				token = await engine.ReadTokenAsync (cancellationToken).ConfigureAwait (false);

				if (token.Type == ImapTokenType.CloseParen)
					break;

				// Note: As seen in https://github.com/jstedfast/MailKit/issues/991, it seems that SmarterMail IMAP
				// servers will sometimes include a NIL address token within the address list. Just ignore it.
				if (token.Type == ImapTokenType.Nil)
					continue;

				ImapEngine.AssertToken (token, ImapTokenType.OpenParen, format, token);

				var address = await ParseEnvelopeAddressAsync (engine, format, cancellationToken).ConfigureAwait (false);

				AddEnvelopeAddress (engine, stack, ref sp, address);
			} while (true);
		}

		static DateTimeOffset? ParseEnvelopeDate (ImapEngine engine, string format, CancellationToken cancellationToken)
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

			if (!DateUtils.TryParse (value, out var date))
				return null;

			return date;
		}

		static async Task<DateTimeOffset?> ParseEnvelopeDateAsync (ImapEngine engine, string format, CancellationToken cancellationToken)
		{
			var token = await engine.ReadTokenAsync (cancellationToken).ConfigureAwait (false);
			string value;

			switch (token.Type) {
			case ImapTokenType.Literal:
				value = await engine.ReadLiteralAsync (cancellationToken).ConfigureAwait (false);
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

			if (!DateUtils.TryParse (value, out var date))
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

			ImapEngine.AssertToken (token, ImapTokenType.OpenParen, format, token);

			var envelope = new Envelope ();
			envelope.Date = ParseEnvelopeDate (engine, format, cancellationToken);
			envelope.Subject = ReadNStringToken (engine, format, true, cancellationToken);
			ParseEnvelopeAddressList (envelope.From, engine, format, cancellationToken);
			ParseEnvelopeAddressList (envelope.Sender, engine, format, cancellationToken);
			ParseEnvelopeAddressList (envelope.ReplyTo, engine, format, cancellationToken);
			ParseEnvelopeAddressList (envelope.To, engine, format, cancellationToken);
			ParseEnvelopeAddressList (envelope.Cc, engine, format, cancellationToken);
			ParseEnvelopeAddressList (envelope.Bcc, engine, format, cancellationToken);

			// Note: Some broken IMAP servers will forget to include the In-Reply-To token (I guess if the header isn't set?).
			//
			// See https://github.com/jstedfast/MailKit/issues/932
			token = engine.PeekToken (cancellationToken);
			if (token.Type != ImapTokenType.CloseParen) {
				if ((nstring = ReadNStringToken (engine, format, false, cancellationToken)) != null)
					envelope.InReplyTo = MimeUtils.EnumerateReferences (nstring).FirstOrDefault ();

				// Note: Some broken IMAP servers will forget to include the Message-Id token (I guess if the header isn't set?).
				//
				// See https://github.com/jstedfast/MailKit/issues/669
				token = engine.PeekToken (cancellationToken);
				if (token.Type != ImapTokenType.CloseParen) {
					if ((nstring = ReadNStringToken (engine, format, false, cancellationToken)) != null) {
						try {
							envelope.MessageId = MimeUtils.ParseMessageId (nstring);
						} catch {
							envelope.MessageId = nstring;
						}
					}
				}
			}

			token = engine.ReadToken (cancellationToken);

			ImapEngine.AssertToken (token, ImapTokenType.CloseParen, format, token);

			return envelope;
		}

		/// <summary>
		/// Parses the ENVELOPE parenthesized list.
		/// </summary>
		/// <returns>The envelope.</returns>
		/// <param name="engine">The IMAP engine.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		public static async Task<Envelope> ParseEnvelopeAsync (ImapEngine engine, CancellationToken cancellationToken)
		{
			string format = string.Format (ImapEngine.GenericItemSyntaxErrorFormat, "ENVELOPE", "{0}");
			var token = await engine.ReadTokenAsync (cancellationToken).ConfigureAwait (false);
			string nstring;

			ImapEngine.AssertToken (token, ImapTokenType.OpenParen, format, token);

			var envelope = new Envelope ();
			envelope.Date = await ParseEnvelopeDateAsync (engine, format, cancellationToken).ConfigureAwait (false);
			envelope.Subject = await ReadNStringTokenAsync (engine, format, true, cancellationToken).ConfigureAwait (false);
			await ParseEnvelopeAddressListAsync (envelope.From, engine, format, cancellationToken).ConfigureAwait (false);
			await ParseEnvelopeAddressListAsync (envelope.Sender, engine, format, cancellationToken).ConfigureAwait (false);
			await ParseEnvelopeAddressListAsync (envelope.ReplyTo, engine, format, cancellationToken).ConfigureAwait (false);
			await ParseEnvelopeAddressListAsync (envelope.To, engine, format, cancellationToken).ConfigureAwait (false);
			await ParseEnvelopeAddressListAsync (envelope.Cc, engine, format, cancellationToken).ConfigureAwait (false);
			await ParseEnvelopeAddressListAsync (envelope.Bcc, engine, format, cancellationToken).ConfigureAwait (false);

			// Note: Some broken IMAP servers will forget to include the In-Reply-To token (I guess if the header isn't set?).
			//
			// See https://github.com/jstedfast/MailKit/issues/932
			token = await engine.PeekTokenAsync (cancellationToken).ConfigureAwait (false);
			if (token.Type != ImapTokenType.CloseParen) {
				if ((nstring = await ReadNStringTokenAsync (engine, format, false, cancellationToken).ConfigureAwait (false)) != null)
					envelope.InReplyTo = MimeUtils.EnumerateReferences (nstring).FirstOrDefault ();

				// Note: Some broken IMAP servers will forget to include the Message-Id token (I guess if the header isn't set?).
				//
				// See https://github.com/jstedfast/MailKit/issues/669
				token = await engine.PeekTokenAsync (cancellationToken).ConfigureAwait (false);
				if (token.Type != ImapTokenType.CloseParen) {
					if ((nstring = await ReadNStringTokenAsync (engine, format, false, cancellationToken).ConfigureAwait (false)) != null) {
						try {
							envelope.MessageId = MimeUtils.ParseMessageId (nstring);
						} catch {
							envelope.MessageId = nstring;
						}
					}
				}
			}

			token = await engine.ReadTokenAsync (cancellationToken).ConfigureAwait (false);

			ImapEngine.AssertToken (token, ImapTokenType.CloseParen, format, token);

			return envelope;
		}

		/// <summary>
		/// Formats a flags list suitable for use with the APPEND command.
		/// </summary>
		/// <param name="flags">The message flags.</param>
		/// <param name="builder">The string builder.</param>
		/// <param name="numKeywords">The number of keywords.</param>
		public static void FormatFlagsList (StringBuilder builder, MessageFlags flags, int numKeywords)
		{
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

			for (int i = 0; i < numKeywords; i++)
				builder.Append ("%S ");

			if (builder.Length > 1)
				builder.Length--;

			builder.Append (')');
		}

		/// <summary>
		/// Formats a flags list suitable for use with the APPEND command.
		/// </summary>
		/// <returns>The flags list string.</returns>
		/// <param name="flags">The message flags.</param>
		/// <param name="numKeywords">The number of keywords.</param>
		public static string FormatFlagsList (MessageFlags flags, int numKeywords)
		{
			var builder = new StringBuilder ();

			FormatFlagsList (builder, flags, numKeywords);

			return builder.ToString ();
		}

		static void AddFlag (ImapToken token, ref MessageFlags flags, HashSet<string> keywords)
		{
			if (token.Type != ImapTokenType.Nil) {
				var flag = (string) token.Value;

				if (flag.Equals ("\\Answered", StringComparison.OrdinalIgnoreCase))
					flags |= MessageFlags.Answered;
				else if (flag.Equals ("\\Deleted", StringComparison.OrdinalIgnoreCase))
					flags |= MessageFlags.Deleted;
				else if (flag.Equals ("\\Draft", StringComparison.OrdinalIgnoreCase))
					flags |= MessageFlags.Draft;
				else if (flag.Equals ("\\Flagged", StringComparison.OrdinalIgnoreCase))
					flags |= MessageFlags.Flagged;
				else if (flag.Equals ("\\Seen", StringComparison.OrdinalIgnoreCase))
					flags |= MessageFlags.Seen;
				else if (flag.Equals ("\\Recent", StringComparison.OrdinalIgnoreCase))
					flags |= MessageFlags.Recent;
				else if (flag.Equals ("\\*", StringComparison.OrdinalIgnoreCase))
					flags |= MessageFlags.UserDefined;
				else if (keywords != null)
					keywords.Add (flag);
			}
		}

		/// <summary>
		/// Parses the flags list.
		/// </summary>
		/// <returns>The message flags.</returns>
		/// <param name="engine">The IMAP engine.</param>
		/// <param name="name">The name of the flags being parsed.</param>
		/// <param name="keywords">A hash set of user-defined message flags that will be populated if non-null.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		public static MessageFlags ParseFlagsList (ImapEngine engine, string name, HashSet<string> keywords, CancellationToken cancellationToken)
		{
			var token = engine.ReadToken (cancellationToken);
			var flags = MessageFlags.None;

			ImapEngine.AssertToken (token, ImapTokenType.OpenParen, ImapEngine.GenericItemSyntaxErrorFormat, name, token);

			token = engine.ReadToken (ImapStream.AtomSpecials, cancellationToken);

			while (token.Type == ImapTokenType.Atom || token.Type == ImapTokenType.Flag || token.Type == ImapTokenType.QString || token.Type == ImapTokenType.Nil) {
				AddFlag (token, ref flags, keywords);

				token = engine.ReadToken (ImapStream.AtomSpecials, cancellationToken);
			}

			ImapEngine.AssertToken (token, ImapTokenType.CloseParen, ImapEngine.GenericItemSyntaxErrorFormat, name, token);

			return flags;
		}

		/// <summary>
		/// Parses the flags list.
		/// </summary>
		/// <returns>The message flags.</returns>
		/// <param name="engine">The IMAP engine.</param>
		/// <param name="name">The name of the flags being parsed.</param>
		/// <param name="keywords">A hash set of user-defined message flags that will be populated if non-null.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		public static async Task<MessageFlags> ParseFlagsListAsync (ImapEngine engine, string name, HashSet<string> keywords, CancellationToken cancellationToken)
		{
			var token = await engine.ReadTokenAsync (cancellationToken).ConfigureAwait (false);
			var flags = MessageFlags.None;

			ImapEngine.AssertToken (token, ImapTokenType.OpenParen, ImapEngine.GenericItemSyntaxErrorFormat, name, token);

			token = await engine.ReadTokenAsync (ImapStream.AtomSpecials, cancellationToken).ConfigureAwait (false);

			while (token.Type == ImapTokenType.Atom || token.Type == ImapTokenType.Flag || token.Type == ImapTokenType.QString || token.Type == ImapTokenType.Nil) {
				AddFlag (token, ref flags, keywords);

				token = await engine.ReadTokenAsync (ImapStream.AtomSpecials, cancellationToken).ConfigureAwait (false);
			}

			ImapEngine.AssertToken (token, ImapTokenType.CloseParen, ImapEngine.GenericItemSyntaxErrorFormat, name, token);

			return flags;
		}

		/// <summary>
		/// Parses the ANNOTATION list.
		/// </summary>
		/// <returns>The list of annotations.</returns>
		/// <param name="engine">The IMAP engine.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		public static ReadOnlyCollection<Annotation> ParseAnnotations (ImapEngine engine, CancellationToken cancellationToken)
		{
			var format = string.Format (ImapEngine.GenericUntaggedResponseSyntaxErrorFormat, "ANNOTATION", "{0}");
			var token = engine.ReadToken (cancellationToken);
			var annotations = new List<Annotation> ();

			ImapEngine.AssertToken (token, ImapTokenType.OpenParen, ImapEngine.GenericItemSyntaxErrorFormat, "ANNOTATION", token);

			do {
				token = engine.PeekToken (ImapStream.AtomSpecials, cancellationToken);

				if (token.Type == ImapTokenType.CloseParen)
					break;

				var path = ReadStringToken (engine, format, cancellationToken);
				var entry = AnnotationEntry.Parse (path);
				var annotation = new Annotation (entry);

				annotations.Add (annotation);

				token = engine.PeekToken (cancellationToken);

				// Note: Unsolicited FETCH responses that include ANNOTATION data do not include attribute values.
				if (token.Type == ImapTokenType.OpenParen) {
					// consume the '('
					engine.ReadToken (cancellationToken);

					// read the attribute/value pairs
					do {
						token = engine.PeekToken (ImapStream.AtomSpecials, cancellationToken);

						if (token.Type == ImapTokenType.CloseParen)
							break;

						var name = ReadStringToken (engine, format, cancellationToken);
						var value = ReadNStringToken (engine, format, false, cancellationToken);
						var attribute = new AnnotationAttribute (name);

						annotation.Properties[attribute] = value;
					} while (true);

					// consume the ')'
					engine.ReadToken (cancellationToken);
				}
			} while (true);

			// consume the ')'
			engine.ReadToken (cancellationToken);

			return new ReadOnlyCollection<Annotation> (annotations);
		}

		/// <summary>
		/// Parses the ANNOTATION list.
		/// </summary>
		/// <returns>The list of annotations.</returns>
		/// <param name="engine">The IMAP engine.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		public static async Task<ReadOnlyCollection<Annotation>> ParseAnnotationsAsync (ImapEngine engine, CancellationToken cancellationToken)
		{
			var format = string.Format (ImapEngine.GenericUntaggedResponseSyntaxErrorFormat, "ANNOTATION", "{0}");
			var token = await engine.ReadTokenAsync (cancellationToken).ConfigureAwait (false);
			var annotations = new List<Annotation> ();

			ImapEngine.AssertToken (token, ImapTokenType.OpenParen, ImapEngine.GenericItemSyntaxErrorFormat, "ANNOTATION", token);

			do {
				token = await engine.PeekTokenAsync (ImapStream.AtomSpecials, cancellationToken).ConfigureAwait (false);

				if (token.Type == ImapTokenType.CloseParen)
					break;

				var path = await ReadStringTokenAsync (engine, format, cancellationToken).ConfigureAwait (false);
				var entry = AnnotationEntry.Parse (path);
				var annotation = new Annotation (entry);

				annotations.Add (annotation);

				token = await engine.PeekTokenAsync (cancellationToken).ConfigureAwait (false);

				// Note: Unsolicited FETCH responses that include ANNOTATION data do not include attribute values.
				if (token.Type == ImapTokenType.OpenParen) {
					// consume the '('
					await engine.ReadTokenAsync (cancellationToken).ConfigureAwait (false);

					// read the attribute/value pairs
					do {
						token = await engine.PeekTokenAsync (ImapStream.AtomSpecials, cancellationToken).ConfigureAwait (false);

						if (token.Type == ImapTokenType.CloseParen)
							break;

						var name = await ReadStringTokenAsync (engine, format, cancellationToken).ConfigureAwait (false);
						var value = await ReadNStringTokenAsync (engine, format, false, cancellationToken).ConfigureAwait (false);
						var attribute = new AnnotationAttribute (name);

						annotation.Properties[attribute] = value;
					} while (true);

					// consume the ')'
					await engine.ReadTokenAsync (cancellationToken).ConfigureAwait (false);
				}
			} while (true);

			// consume the ')'
			await engine.ReadTokenAsync (cancellationToken).ConfigureAwait (false);

			return new ReadOnlyCollection<Annotation> (annotations);
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

			ImapEngine.AssertToken (token, ImapTokenType.OpenParen, ImapEngine.GenericItemSyntaxErrorFormat, "X-GM-LABELS", token);

			token = engine.ReadToken (ImapStream.AtomSpecials, cancellationToken);

			while (token.Type == ImapTokenType.Flag || token.Type == ImapTokenType.Atom || token.Type == ImapTokenType.QString || token.Type == ImapTokenType.Nil) {
				// Apparently it's possible to set a NIL label in GMail...
				//
				// See https://github.com/jstedfast/MailKit/issues/244 for an example.
				if (token.Type != ImapTokenType.Nil) {
					var label = engine.DecodeMailboxName ((string) token.Value);

					labels.Add (label);
				} else {
					labels.Add ((string) token.Value);
				}

				token = engine.ReadToken (ImapStream.AtomSpecials, cancellationToken);
			}

			ImapEngine.AssertToken (token, ImapTokenType.CloseParen, ImapEngine.GenericItemSyntaxErrorFormat, "X-GM-LABELS", token);

			return new ReadOnlyCollection<string> (labels);
		}

		/// <summary>
		/// Parses the X-GM-LABELS list.
		/// </summary>
		/// <returns>The message labels.</returns>
		/// <param name="engine">The IMAP engine.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		public static async Task<ReadOnlyCollection<string>> ParseLabelsListAsync (ImapEngine engine, CancellationToken cancellationToken)
		{
			var token = await engine.ReadTokenAsync (cancellationToken).ConfigureAwait (false);
			var labels = new List<string> ();

			ImapEngine.AssertToken (token, ImapTokenType.OpenParen, ImapEngine.GenericItemSyntaxErrorFormat, "X-GM-LABELS", token);

			token = await engine.ReadTokenAsync (ImapStream.AtomSpecials, cancellationToken).ConfigureAwait (false);

			while (token.Type == ImapTokenType.Flag || token.Type == ImapTokenType.Atom || token.Type == ImapTokenType.QString || token.Type == ImapTokenType.Nil) {
				// Apparently it's possible to set a NIL label in GMail...
				//
				// See https://github.com/jstedfast/MailKit/issues/244 for an example.
				if (token.Type != ImapTokenType.Nil) {
					var label = engine.DecodeMailboxName ((string) token.Value);

					labels.Add (label);
				} else {
					labels.Add ((string) token.Value);
				}

				token = await engine.ReadTokenAsync (ImapStream.AtomSpecials, cancellationToken).ConfigureAwait (false);
			}

			ImapEngine.AssertToken (token, ImapTokenType.CloseParen, ImapEngine.GenericItemSyntaxErrorFormat, "X-GM-LABELS", token);

			return new ReadOnlyCollection<string> (labels);
		}

		static MessageThread ParseThread (ImapEngine engine, uint uidValidity, CancellationToken cancellationToken)
		{
			var token = engine.ReadToken (cancellationToken);
			MessageThread thread, node, child;
			uint uid;

			if (token.Type == ImapTokenType.OpenParen) {
				thread = new MessageThread ((UniqueId?) null /*UniqueId.Invalid*/);

				do {
					child = ParseThread (engine, uidValidity, cancellationToken);
					thread.Children.Add (child);

					token = engine.ReadToken (cancellationToken);
				} while (token.Type != ImapTokenType.CloseParen);

				return thread;
			}

			uid = ImapEngine.ParseNumber (token, true, ImapEngine.GenericUntaggedResponseSyntaxErrorFormat, "THREAD", token);
			node = thread = new MessageThread (new UniqueId (uidValidity, uid));

			do {
				token = engine.ReadToken (cancellationToken);

				if (token.Type == ImapTokenType.CloseParen)
					break;

				if (token.Type == ImapTokenType.OpenParen) {
					child = ParseThread (engine, uidValidity, cancellationToken);
					node.Children.Add (child);
				} else {
					uid = ImapEngine.ParseNumber (token, true, ImapEngine.GenericUntaggedResponseSyntaxErrorFormat, "THREAD", token);
					child = new MessageThread (new UniqueId (uidValidity, uid));
					node.Children.Add (child);
					node = child;
				}
			} while (true);

			return thread;
		}

		static async Task<MessageThread> ParseThreadAsync (ImapEngine engine, uint uidValidity, CancellationToken cancellationToken)
		{
			var token = await engine.ReadTokenAsync (cancellationToken).ConfigureAwait (false);
			MessageThread thread, node, child;
			uint uid;

			if (token.Type == ImapTokenType.OpenParen) {
				thread = new MessageThread ((UniqueId?) null /*UniqueId.Invalid*/);

				do {
					child = await ParseThreadAsync (engine, uidValidity, cancellationToken).ConfigureAwait (false);
					thread.Children.Add (child);

					token = await engine.ReadTokenAsync (cancellationToken).ConfigureAwait (false);
				} while (token.Type != ImapTokenType.CloseParen);

				return thread;
			}

			uid = ImapEngine.ParseNumber (token, true, ImapEngine.GenericUntaggedResponseSyntaxErrorFormat, "THREAD", token);
			node = thread = new MessageThread (new UniqueId (uidValidity, uid));

			do {
				token = await engine.ReadTokenAsync (cancellationToken).ConfigureAwait (false);

				if (token.Type == ImapTokenType.CloseParen)
					break;

				if (token.Type == ImapTokenType.OpenParen) {
					child = await ParseThreadAsync (engine, uidValidity, cancellationToken).ConfigureAwait (false);
					node.Children.Add (child);
				} else {
					uid = ImapEngine.ParseNumber (token, true, ImapEngine.GenericUntaggedResponseSyntaxErrorFormat, "THREAD", token);
					child = new MessageThread (new UniqueId (uidValidity, uid));
					node.Children.Add (child);
					node = child;
				}
			} while (true);

			return thread;
		}

		/// <summary>
		/// Parses an untagged THREAD response.
		/// </summary>
		/// <returns>The task.</returns>
		/// <param name="engine">The IMAP engine.</param>
		/// <param name="uidValidity">The UIDVALIDITY of the folder.</param>
		/// <param name="threads">The list of message threads that this method will append to.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		public static void ParseThreads (ImapEngine engine, uint uidValidity, List<MessageThread> threads, CancellationToken cancellationToken)
		{
			ImapToken token;

			do {
				token = engine.PeekToken (cancellationToken);

				if (token.Type == ImapTokenType.Eoln)
					break;

				token = engine.ReadToken (cancellationToken);

				ImapEngine.AssertToken (token, ImapTokenType.OpenParen, ImapEngine.GenericUntaggedResponseSyntaxErrorFormat, "THREAD", token);

				threads.Add (ParseThread (engine, uidValidity, cancellationToken));
			} while (true);
		}

		/// <summary>
		/// Parses the threads.
		/// </summary>
		/// <returns>The task.</returns>
		/// <param name="engine">The IMAP engine.</param>
		/// <param name="uidValidity">The UIDVALIDITY of the folder.</param>
		/// <param name="threads">THe list of message threads that will be appended to.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		public static async Task ParseThreadsAsync (ImapEngine engine, uint uidValidity, List<MessageThread> threads, CancellationToken cancellationToken)
		{
			ImapToken token;

			do {
				token = await engine.PeekTokenAsync (cancellationToken).ConfigureAwait (false);

				if (token.Type == ImapTokenType.Eoln)
					break;

				token = await engine.ReadTokenAsync (cancellationToken).ConfigureAwait (false);

				ImapEngine.AssertToken (token, ImapTokenType.OpenParen, ImapEngine.GenericUntaggedResponseSyntaxErrorFormat, "THREAD", token);

				threads.Add (await ParseThreadAsync (engine, uidValidity, cancellationToken).ConfigureAwait (false));
			} while (true);
		}

		/// <summary>
		/// Handles an untagged THREAD response.
		/// </summary>
		/// <returns>The task.</returns>
		/// <param name="engine">The IMAP engine.</param>
		/// <param name="ic">The IMAP command.</param>
		/// <param name="index">THe index.</param>
		/// <param name="doAsync">Whether or not asynchronous IO methods should be used.</param>
		public static Task UntaggedThreadHandler (ImapEngine engine, ImapCommand ic, int index, bool doAsync)
		{
			var threads = new List<MessageThread> ();

			ic.UserData = threads;

			if (doAsync)
				return ParseThreadsAsync (engine, ic.Folder.UidValidity, threads, ic.CancellationToken);

			ParseThreads (engine, ic.Folder.UidValidity, threads, ic.CancellationToken);

			return Task.CompletedTask;
		}
	}
}
