//
// ImapFolderFetch.cs
//
// Author: Jeffrey Stedfast <jestedfa@microsoft.com>
//
// Copyright (c) 2013-2020 .NET Foundation and Contributors
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
using System.Buffers;
using System.Threading;
using System.Globalization;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Collections.ObjectModel;

using MimeKit;
using MimeKit.IO;
using MimeKit.Text;
using MimeKit.Utils;

using MailKit.Search;

namespace MailKit.Net.Imap
{
	public partial class ImapFolder
	{
		internal static readonly HashSet<string> EmptyHeaderFields = new HashSet<string> ();
		const int PreviewHtmlLength = 16 * 1024;
		const int PreviewTextLength = 512;
		const int BufferSize = 4096;

		class FetchSummaryContext
		{
			public readonly List<IMessageSummary> Messages;

			public FetchSummaryContext ()
			{
				Messages = new List<IMessageSummary> ();
			}

			int BinarySearch (int index, bool insert)
			{
				int min = 0, max = Messages.Count;

				if (max == 0)
					return insert ? 0 : -1;

				do {
					int i = min + ((max - min) / 2);

					if (index == Messages[i].Index)
						return i;

					if (index > Messages[i].Index) {
						min = i + 1;
					} else {
						max = i;
					}
				} while (min < max);

				return insert ? min : -1;
			}

			public void Add (int index, MessageSummary message)
			{
				int i = BinarySearch (index, true);

				if (i < Messages.Count)
					Messages.Insert (i, message);
				else
					Messages.Add (message);
			}

			public bool TryGetValue (int index, out MessageSummary message)
			{
				int i;

				if ((i = BinarySearch (index, false)) == -1) {
					message = null;
					return false;
				}

				message = (MessageSummary) Messages[i];

				return true;
			}

			public void OnMessageExpunged (object sender, MessageEventArgs args)
			{
				int index = BinarySearch (args.Index, true);

				if (index >= Messages.Count)
					return;

				if (Messages[index].Index == args.Index)
					Messages.RemoveAt (index);

				for (int i = index; i < Messages.Count; i++) {
					var message = (MessageSummary) Messages[i];
					message.Index--;
				}
			}
		}

		static async Task ReadLiteralDataAsync (ImapEngine engine, bool doAsync, CancellationToken cancellationToken)
		{
			var buf = ArrayPool<byte>.Shared.Rent (BufferSize);
			int nread;

			try {
				do {
					if (doAsync)
						nread = await engine.Stream.ReadAsync (buf, 0, BufferSize, cancellationToken).ConfigureAwait (false);
					else
						nread = engine.Stream.Read (buf, 0, BufferSize, cancellationToken);
				} while (nread > 0);
			} finally {
				ArrayPool<byte>.Shared.Return (buf);
			}
		}

		static async Task SkipParenthesizedList (ImapEngine engine, bool doAsync, CancellationToken cancellationToken)
		{
			do {
				var token = await engine.PeekTokenAsync (doAsync, cancellationToken).ConfigureAwait (false);

				if (token.Type == ImapTokenType.Eoln)
					return;

				// token is safe to read, so pop it off the queue
				await engine.ReadTokenAsync (doAsync, cancellationToken).ConfigureAwait (false);

				if (token.Type == ImapTokenType.CloseParen)
					break;

				if (token.Type == ImapTokenType.OpenParen) {
					// skip the inner parenthesized list
					await SkipParenthesizedList (engine, doAsync, cancellationToken).ConfigureAwait (false);
				}
			} while (true);
		}

		async Task<DateTimeOffset?> ReadDateTimeOffsetTokenAsync (ImapEngine engine, string atom, bool doAsync, CancellationToken cancellationToken)
		{
			var token = await engine.ReadTokenAsync (doAsync, cancellationToken).ConfigureAwait (false);

			switch (token.Type) {
			case ImapTokenType.QString:
			case ImapTokenType.Atom:
				return ImapUtils.ParseInternalDate ((string) token.Value);
			case ImapTokenType.Nil:
				return null;
			default:
				throw ImapEngine.UnexpectedToken (ImapEngine.GenericItemSyntaxErrorFormat, atom, token);
			}
		}

		async Task FetchSummaryItemsAsync (ImapEngine engine, MessageSummary message, bool doAsync, CancellationToken cancellationToken)
		{
			var token = await engine.ReadTokenAsync (doAsync, cancellationToken).ConfigureAwait (false);

			ImapEngine.AssertToken (token, ImapTokenType.OpenParen, ImapEngine.GenericUntaggedResponseSyntaxErrorFormat, "FETCH", token);

			do {
				token = await engine.ReadTokenAsync (doAsync, cancellationToken).ConfigureAwait (false);

				if (token.Type == ImapTokenType.CloseParen || token.Type == ImapTokenType.Eoln)
					break;

				bool parenthesized = false;
				if (engine.QuirksMode == ImapQuirksMode.Domino && token.Type == ImapTokenType.OpenParen) {
					// Note: Lotus Domino IMAP will (sometimes?) encapsulate the `ENVELOPE` segment of the
					// response within an extra set of parenthesis.
					//
					// See https://github.com/jstedfast/MailKit/issues/943 for details.
					token = await engine.ReadTokenAsync (doAsync, cancellationToken).ConfigureAwait (false);
					parenthesized = true;
				}

				ImapEngine.AssertToken (token, ImapTokenType.Atom, ImapEngine.GenericUntaggedResponseSyntaxErrorFormat, "FETCH", token);

				var atom = (string) token.Value;
				string format;
				ulong value64;
				uint value;
				int idx;

				switch (atom.ToUpperInvariant ()) {
				case "INTERNALDATE":
					message.InternalDate = await ReadDateTimeOffsetTokenAsync (engine, atom, doAsync, cancellationToken).ConfigureAwait (false);
					message.Fields |= MessageSummaryItems.InternalDate;
					break;
				case "SAVEDATE":
					message.SaveDate = await ReadDateTimeOffsetTokenAsync (engine, atom, doAsync, cancellationToken).ConfigureAwait (false);
					message.Fields |= MessageSummaryItems.SaveDate;
					break;
				case "RFC822.SIZE":
					token = await engine.ReadTokenAsync (doAsync, cancellationToken).ConfigureAwait (false);

					message.Size = ImapEngine.ParseNumber (token, false, ImapEngine.GenericItemSyntaxErrorFormat, atom, token);
					message.Fields |= MessageSummaryItems.Size;
					break;
				case "BODYSTRUCTURE":
					format = string.Format (ImapEngine.GenericItemSyntaxErrorFormat, "BODYSTRUCTURE", "{0}");
					message.Body = await ImapUtils.ParseBodyAsync (engine, format, string.Empty, doAsync, cancellationToken).ConfigureAwait (false);
					message.Fields |= MessageSummaryItems.BodyStructure;
					break;
				case "BODY":
					token = await engine.PeekTokenAsync (doAsync, cancellationToken).ConfigureAwait (false);
					format = ImapEngine.FetchBodySyntaxErrorFormat;

					if (token.Type == ImapTokenType.OpenBracket) {
						var referencesField = false;
						var headerFields = false;

						// consume the '['
						token = await engine.ReadTokenAsync (doAsync, cancellationToken).ConfigureAwait (false);

						ImapEngine.AssertToken (token, ImapTokenType.OpenBracket, format, token);

						// References and/or other headers were requested...

						do {
							token = await engine.ReadTokenAsync (doAsync, cancellationToken).ConfigureAwait (false);

							if (token.Type == ImapTokenType.CloseBracket)
								break;

							if (token.Type == ImapTokenType.OpenParen) {
								do {
									token = await engine.ReadTokenAsync (doAsync, cancellationToken).ConfigureAwait (false);

									if (token.Type == ImapTokenType.CloseParen)
										break;

									// the header field names will generally be atoms or qstrings but may also be literals
									engine.Stream.UngetToken (token);

									var field = await ImapUtils.ReadStringTokenAsync (engine, format, doAsync, cancellationToken).ConfigureAwait (false);

									if (headerFields && !referencesField && field.Equals ("REFERENCES", StringComparison.OrdinalIgnoreCase))
										referencesField = true;
								} while (true);
							} else {
								ImapEngine.AssertToken (token, ImapTokenType.Atom, format, token);

								atom = (string) token.Value;

								headerFields = atom.Equals ("HEADER.FIELDS", StringComparison.OrdinalIgnoreCase);

								if (!headerFields && atom.Equals ("HEADER", StringComparison.OrdinalIgnoreCase)) {
									// if we're fetching *all* headers, then it will include the References header (if it exists)
									referencesField = true;
								}
							}
						} while (true);

						ImapEngine.AssertToken (token, ImapTokenType.CloseBracket, format, token);

						token = await engine.ReadTokenAsync (doAsync, cancellationToken).ConfigureAwait (false);

						ImapEngine.AssertToken (token, ImapTokenType.Literal, format, token);

						try {
							message.Headers = await engine.ParseHeadersAsync (engine.Stream, doAsync, cancellationToken).ConfigureAwait (false);
						} catch (FormatException) {
							message.Headers = new HeaderList ();
						}

						// consume any remaining literal data... (typically extra blank lines)
						await ReadLiteralDataAsync (engine, doAsync, cancellationToken).ConfigureAwait (false);

						message.References = new MessageIdList ();

						if ((idx = message.Headers.IndexOf (HeaderId.References)) != -1) {
							var references = message.Headers[idx];
							var rawValue = references.RawValue;

							foreach (var msgid in MimeUtils.EnumerateReferences (rawValue, 0, rawValue.Length))
								message.References.Add (msgid);
						}

						message.Fields |= MessageSummaryItems.Headers;

						if (referencesField)
							message.Fields |= MessageSummaryItems.References;
					} else {
						message.Body = await ImapUtils.ParseBodyAsync (engine, format, string.Empty, doAsync, cancellationToken).ConfigureAwait (false);
						message.Fields |= MessageSummaryItems.Body;
					}
					break;
				case "ENVELOPE":
					message.Envelope = await ImapUtils.ParseEnvelopeAsync (engine, doAsync, cancellationToken).ConfigureAwait (false);
					message.Fields |= MessageSummaryItems.Envelope;
					break;
				case "FLAGS":
					message.Flags = await ImapUtils.ParseFlagsListAsync (engine, atom, message.Keywords, doAsync, cancellationToken).ConfigureAwait (false);
					message.Fields |= MessageSummaryItems.Flags;
					break;
				case "MODSEQ":
					token = await engine.ReadTokenAsync (doAsync, cancellationToken).ConfigureAwait (false);

					ImapEngine.AssertToken (token, ImapTokenType.OpenParen, ImapEngine.GenericItemSyntaxErrorFormat, atom, token);

					token = await engine.ReadTokenAsync (doAsync, cancellationToken).ConfigureAwait (false);

					value64 = ImapEngine.ParseNumber64 (token, false, ImapEngine.GenericItemSyntaxErrorFormat, atom, token);

					token = await engine.ReadTokenAsync (doAsync, cancellationToken).ConfigureAwait (false);

					ImapEngine.AssertToken (token, ImapTokenType.CloseParen, ImapEngine.GenericItemSyntaxErrorFormat, atom, token);

					message.Fields |= MessageSummaryItems.ModSeq;
					message.ModSeq = value64;

					if (value64 > HighestModSeq)
						UpdateHighestModSeq (value64);
					break;
				case "UID":
					token = await engine.ReadTokenAsync (doAsync, cancellationToken).ConfigureAwait (false);

					value = ImapEngine.ParseNumber (token, true, ImapEngine.GenericItemSyntaxErrorFormat, atom, token);

					message.UniqueId = new UniqueId (UidValidity, value);
					message.Fields |= MessageSummaryItems.UniqueId;
					break;
				case "EMAILID":
					token = await engine.ReadTokenAsync (doAsync, cancellationToken).ConfigureAwait (false);

					ImapEngine.AssertToken (token, ImapTokenType.OpenParen, ImapEngine.GenericItemSyntaxErrorFormat, atom, token);

					token = await engine.ReadTokenAsync (doAsync, cancellationToken).ConfigureAwait (false);

					ImapEngine.AssertToken (token, ImapTokenType.Atom, ImapEngine.GenericItemSyntaxErrorFormat, atom, token);

					message.Fields |= MessageSummaryItems.EmailId;
					message.EmailId = (string) token.Value;

					token = await engine.ReadTokenAsync (doAsync, cancellationToken).ConfigureAwait (false);

					ImapEngine.AssertToken (token, ImapTokenType.CloseParen, ImapEngine.GenericItemSyntaxErrorFormat, atom, token);
					break;
				case "THREADID":
					token = await engine.ReadTokenAsync (doAsync, cancellationToken).ConfigureAwait (false);

					if (token.Type == ImapTokenType.OpenParen) {
						token = await engine.ReadTokenAsync (doAsync, cancellationToken).ConfigureAwait (false);

						ImapEngine.AssertToken (token, ImapTokenType.Atom, ImapEngine.GenericItemSyntaxErrorFormat, atom, token);

						message.Fields |= MessageSummaryItems.ThreadId;
						message.ThreadId = (string) token.Value;

						token = await engine.ReadTokenAsync (doAsync, cancellationToken).ConfigureAwait (false);

						ImapEngine.AssertToken (token, ImapTokenType.CloseParen, ImapEngine.GenericItemSyntaxErrorFormat, atom, token);
					} else {
						ImapEngine.AssertToken (token, ImapTokenType.Nil, ImapEngine.GenericItemSyntaxErrorFormat, atom, token);

						message.Fields |= MessageSummaryItems.ThreadId;
						message.ThreadId = null;
					}
					break;
				case "X-GM-MSGID":
					token = await engine.ReadTokenAsync (doAsync, cancellationToken).ConfigureAwait (false);

					value64 = ImapEngine.ParseNumber64 (token, true, ImapEngine.GenericItemSyntaxErrorFormat, atom, token);
					message.Fields |= MessageSummaryItems.GMailMessageId;
					message.GMailMessageId = value64;
					break;
				case "X-GM-THRID":
					token = await engine.ReadTokenAsync (doAsync, cancellationToken).ConfigureAwait (false);

					value64 = ImapEngine.ParseNumber64 (token, true, ImapEngine.GenericItemSyntaxErrorFormat, atom, token);
					message.Fields |= MessageSummaryItems.GMailThreadId;
					message.GMailThreadId = value64;
					break;
				case "X-GM-LABELS":
					message.GMailLabels = await ImapUtils.ParseLabelsListAsync (engine, doAsync, cancellationToken).ConfigureAwait (false);
					message.Fields |= MessageSummaryItems.GMailLabels;
					break;
				case "ANNOTATION":
					message.Annotations = await ImapUtils.ParseAnnotationsAsync (engine, doAsync, cancellationToken).ConfigureAwait (false);
					message.Fields |= MessageSummaryItems.Annotations;
					break;
				default:
					// Unexpected or unknown token (such as XAOL.SPAM.REASON or XAOL-MSGID). Simply read 1 more token (the argument) and ignore.
					token = await engine.ReadTokenAsync (doAsync, cancellationToken).ConfigureAwait (false);

					if (token.Type == ImapTokenType.OpenParen)
						await SkipParenthesizedList (engine, doAsync, cancellationToken).ConfigureAwait (false);
					break;
				}

				if (parenthesized) {
					// Note: This is the second half of the Lotus Domino IMAP server work-around.
					token = await engine.ReadTokenAsync (doAsync, cancellationToken).ConfigureAwait (false);
					ImapEngine.AssertToken (token, ImapTokenType.CloseParen, ImapEngine.GenericUntaggedResponseSyntaxErrorFormat, "FETCH", token);
				}
			} while (true);

			ImapEngine.AssertToken (token, ImapTokenType.CloseParen, ImapEngine.GenericUntaggedResponseSyntaxErrorFormat, "FETCH", token);
		}

		async Task FetchSummaryItemsAsync (ImapEngine engine, ImapCommand ic, int index, bool doAsync)
		{
			var ctx = (FetchSummaryContext) ic.UserData;
			MessageSummary message;

			if (!ctx.TryGetValue (index, out message)) {
				message = new MessageSummary (this, index);
				ctx.Add (index, message);
			}

			await FetchSummaryItemsAsync (engine, message, doAsync, ic.CancellationToken).ConfigureAwait (false);

			OnMessageSummaryFetched (message);
		}

		internal static string FormatSummaryItems (ImapEngine engine, ref MessageSummaryItems items, HashSet<string> headers, out bool previewText, bool isNotify = false)
		{
			if ((items & MessageSummaryItems.PreviewText) != 0) {
				// if the user wants the preview text, we will also need the UIDs and BODYSTRUCTUREs
				// so that we can request a preview of the body text in subsequent FETCH requests.
				items |= MessageSummaryItems.BodyStructure | MessageSummaryItems.UniqueId;
				previewText = true;
			} else {
				previewText = false;
			}

			if ((items & MessageSummaryItems.BodyStructure) != 0 && (items & MessageSummaryItems.Body) != 0) {
				// don't query both the BODY and BODYSTRUCTURE, that's just dumb...
				items &= ~MessageSummaryItems.Body;
			}

			if (engine.QuirksMode != ImapQuirksMode.GMail && !isNotify) {
				// first, eliminate the aliases...
				var alias = items & ~MessageSummaryItems.PreviewText;

				if (alias == MessageSummaryItems.All)
					return "ALL";

				if (alias == MessageSummaryItems.Full)
					return "FULL";

				if (alias == MessageSummaryItems.Fast)
					return "FAST";
			}

			var tokens = new List<string> ();

			// now add on any additional summary items...
			if ((items & MessageSummaryItems.UniqueId) != 0)
				tokens.Add ("UID");
			if ((items & MessageSummaryItems.Flags) != 0)
				tokens.Add ("FLAGS");
			if ((items & MessageSummaryItems.InternalDate) != 0)
				tokens.Add ("INTERNALDATE");
			if ((items & MessageSummaryItems.Size) != 0)
				tokens.Add ("RFC822.SIZE");
			if ((items & MessageSummaryItems.Envelope) != 0)
				tokens.Add ("ENVELOPE");
			if ((items & MessageSummaryItems.BodyStructure) != 0)
				tokens.Add ("BODYSTRUCTURE");
			if ((items & MessageSummaryItems.Body) != 0)
				tokens.Add ("BODY");

			if ((engine.Capabilities & ImapCapabilities.CondStore) != 0) {
				if ((items & MessageSummaryItems.ModSeq) != 0)
					tokens.Add ("MODSEQ");
			}

			if ((engine.Capabilities & ImapCapabilities.Annotate) != 0) {
				if ((items & MessageSummaryItems.Annotations) != 0)
					tokens.Add ("ANNOTATION (/* (value size))");
			}

			if ((engine.Capabilities & ImapCapabilities.ObjectID) != 0) {
				if ((items & MessageSummaryItems.EmailId) != 0)
					tokens.Add ("EMAILID");
				if ((items & MessageSummaryItems.ThreadId) != 0)
					tokens.Add ("THREADID");
			}

			if ((engine.Capabilities & ImapCapabilities.SaveDate) != 0) {
				if ((items & MessageSummaryItems.SaveDate) != 0)
					tokens.Add ("SAVEDATE");
			}

			if ((engine.Capabilities & ImapCapabilities.GMailExt1) != 0) {
				// now for the GMail extension items
				if ((items & MessageSummaryItems.GMailMessageId) != 0)
					tokens.Add ("X-GM-MSGID");
				if ((items & MessageSummaryItems.GMailThreadId) != 0)
					tokens.Add ("X-GM-THRID");
				if ((items & MessageSummaryItems.GMailLabels) != 0)
					tokens.Add ("X-GM-LABELS");
			}

			if ((items & MessageSummaryItems.Headers) != 0) {
				tokens.Add ("BODY.PEEK[HEADER]");
			} else if ((items & MessageSummaryItems.References) != 0 || headers.Count > 0) {
				var headerFields = new StringBuilder ("BODY.PEEK[HEADER.FIELDS (");
				var references = false;

				foreach (var header in headers) {
					if (header.Equals ("REFERENCES", StringComparison.OrdinalIgnoreCase))
						references = true;

					headerFields.Append (header);
					headerFields.Append (' ');
				}

				if ((items & MessageSummaryItems.References) != 0 && !references)
					headerFields.Append ("REFERENCES ");

				headerFields[headerFields.Length - 1] = ')';
				headerFields.Append (']');

				tokens.Add (headerFields.ToString ());
			}

			if (tokens.Count == 1 && !isNotify)
				return tokens[0];

			return string.Format ("({0})", string.Join (" ", tokens));
		}

		string FormatSummaryItems (ref MessageSummaryItems items, HashSet<string> headers, out bool previewText)
		{
			return FormatSummaryItems (Engine, ref items, headers, out previewText);
		}

		static IList<IMessageSummary> AsReadOnly (ICollection<IMessageSummary> collection)
		{
			var array = new IMessageSummary[collection.Count];

			collection.CopyTo (array, 0);

			return new ReadOnlyCollection<IMessageSummary> (array);
		}

		class FetchPreviewTextContext : FetchStreamContextBase
		{
			static readonly PlainTextPreviewer textPreviewer = new PlainTextPreviewer ();
			static readonly HtmlTextPreviewer htmlPreviewer = new HtmlTextPreviewer ();

			readonly FetchSummaryContext ctx;
			readonly ImapFolder folder;

			public FetchPreviewTextContext (ImapFolder folder, FetchSummaryContext ctx) : base (null)
			{
				this.folder = folder;
				this.ctx = ctx;
			}

			public override Task AddAsync (Section section, bool doAsync, CancellationToken cancellationToken)
			{
				MessageSummary message;

				if (!ctx.TryGetValue (section.Index, out message))
					return Complete;

				var body = message.TextBody;
				TextPreviewer previewer;

				if (body == null) {
					previewer = htmlPreviewer;
					body = message.HtmlBody;
				} else {
					previewer = textPreviewer;
				}

				if (body == null)
					return Complete;

				var charset = body.ContentType.Charset ?? "utf-8";
				ContentEncoding encoding;

				if (!string.IsNullOrEmpty (body.ContentTransferEncoding))
					MimeUtils.TryParse (body.ContentTransferEncoding, out encoding);
				else
					encoding = ContentEncoding.Default;

				using (var memory = new MemoryStream ()) {
					var content = new MimeContent (section.Stream, encoding);

					content.DecodeTo (memory);
					memory.Position = 0;

					try {
						message.PreviewText = previewer.GetPreviewText (memory, charset);
					} catch (DecoderFallbackException) {
						memory.Position = 0;

						message.PreviewText = previewer.GetPreviewText (memory, ImapEngine.Latin1);
					}

					message.Fields |= MessageSummaryItems.PreviewText;
					folder.OnMessageSummaryFetched (message);
				}

				return Complete;
			}

			public override Task SetUniqueIdAsync (int index, UniqueId uid, bool doAsync, CancellationToken cancellationToken)
			{
				return Complete;
			}
		}

		async Task FetchPreviewTextAsync (FetchSummaryContext sctx, Dictionary<string, UniqueIdSet> bodies, int octets, bool doAsync, CancellationToken cancellationToken)
		{
			foreach (var pair in bodies) {
				var uids = pair.Value;
				string specifier;

				if (!string.IsNullOrEmpty (pair.Key))
					specifier = pair.Key;
				else
					specifier = "TEXT";

				// TODO: if the IMAP server supports the CONVERT extension, we could possibly use the
				// CONVERT command instead to decode *and* convert (html) into utf-8 plain text.
				//
				// e.g. "UID CONVERT {0} (\"text/plain\" (\"charset\" \"utf-8\")) BINARY[{1}]<0.{2}>\r\n"
				//
				// This would allow us to more accurately fetch X number of characters because we wouldn't
				// need to guestimate accounting for base64/quoted-printable decoding.

				var command = string.Format (CultureInfo.InvariantCulture, "UID FETCH {0} (BODY.PEEK[{1}]<0.{2}>)\r\n", uids, specifier, octets);
				var ic = new ImapCommand (Engine, cancellationToken, this, command);
				var ctx = new FetchPreviewTextContext (this, sctx);

				ic.RegisterUntaggedHandler ("FETCH", FetchStreamAsync);
				ic.UserData = ctx;

				Engine.QueueCommand (ic);

				try {
					await Engine.RunAsync (ic, doAsync).ConfigureAwait (false);

					ProcessResponseCodes (ic, null);

					if (ic.Response != ImapCommandResponse.Ok)
						throw ImapCommandException.Create ("FETCH", ic);
				} finally {
					ctx.Dispose ();
				}
			}
		}

		async Task GetPreviewTextAsync (FetchSummaryContext sctx, bool doAsync, CancellationToken cancellationToken)
		{
			var textBodies = new Dictionary<string, UniqueIdSet> ();
			var htmlBodies = new Dictionary<string, UniqueIdSet> ();

			foreach (var item in sctx.Messages) {
				Dictionary<string, UniqueIdSet> bodies;
				var message = (MessageSummary) item;
				var body = message.TextBody;
				UniqueIdSet uids;

				if (body == null) {
					body = message.HtmlBody;
					bodies = htmlBodies;
				} else {
					bodies = textBodies;
				}

				if (body == null) {
					message.Fields |= MessageSummaryItems.PreviewText;
					message.PreviewText = string.Empty;
					OnMessageSummaryFetched (message);
					continue;
				}

				if (!bodies.TryGetValue (body.PartSpecifier, out uids)) {
					uids = new UniqueIdSet (SortOrder.Ascending);
					bodies.Add (body.PartSpecifier, uids);
				}

				uids.Add (message.UniqueId);
			}

			MessageExpunged += sctx.OnMessageExpunged;

			try {
				await FetchPreviewTextAsync (sctx, textBodies, PreviewTextLength, doAsync, cancellationToken).ConfigureAwait (false);
				await FetchPreviewTextAsync (sctx, htmlBodies, PreviewHtmlLength, doAsync, cancellationToken).ConfigureAwait (false);
			} finally {
				MessageExpunged -= sctx.OnMessageExpunged;
			}
		}

		async Task<IList<IMessageSummary>> FetchAsync (IList<UniqueId> uids, MessageSummaryItems items, bool doAsync, CancellationToken cancellationToken)
		{
			bool previewText;

			if (uids == null)
				throw new ArgumentNullException (nameof (uids));

			if (items == MessageSummaryItems.None)
				throw new ArgumentOutOfRangeException (nameof (items));

			CheckState (true, false);

			if (uids.Count == 0)
				return new IMessageSummary[0];

			var query = FormatSummaryItems (ref items, EmptyHeaderFields, out previewText);
			var command = string.Format ("UID FETCH %s {0}\r\n", query);
			var ctx = new FetchSummaryContext ();

			MessageExpunged += ctx.OnMessageExpunged;

			try {
				foreach (var ic in Engine.CreateCommands (cancellationToken, this, command, uids)) {
					ic.RegisterUntaggedHandler ("FETCH", FetchSummaryItemsAsync);
					ic.UserData = ctx;

					Engine.QueueCommand (ic);

					await Engine.RunAsync (ic, doAsync).ConfigureAwait (false);

					ProcessResponseCodes (ic, null);

					if (ic.Response != ImapCommandResponse.Ok)
						throw ImapCommandException.Create ("FETCH", ic);
				}
			} finally {
				MessageExpunged -= ctx.OnMessageExpunged;
			}

			if (previewText)
				await GetPreviewTextAsync (ctx, doAsync, cancellationToken).ConfigureAwait (false);

			return AsReadOnly (ctx.Messages);
		}

		async Task<IList<IMessageSummary>> FetchAsync (IList<UniqueId> uids, MessageSummaryItems items, IEnumerable<string> headers, bool doAsync, CancellationToken cancellationToken)
		{
			bool previewText;

			if (uids == null)
				throw new ArgumentNullException (nameof (uids));

			var headerFields = ImapUtils.GetUniqueHeaders (headers);

			CheckState (true, false);

			if (uids.Count == 0)
				return new IMessageSummary[0];

			var query = FormatSummaryItems (ref items, headerFields, out previewText);
			var command = string.Format ("UID FETCH %s {0}\r\n", query);
			var ctx = new FetchSummaryContext ();

			MessageExpunged += ctx.OnMessageExpunged;

			try {
				foreach (var ic in Engine.CreateCommands (cancellationToken, this, command, uids)) {
					ic.RegisterUntaggedHandler ("FETCH", FetchSummaryItemsAsync);
					ic.UserData = ctx;

					Engine.QueueCommand (ic);

					await Engine.RunAsync (ic, doAsync).ConfigureAwait (false);

					ProcessResponseCodes (ic, null);

					if (ic.Response != ImapCommandResponse.Ok)
						throw ImapCommandException.Create ("FETCH", ic);
				}
			} finally {
				MessageExpunged -= ctx.OnMessageExpunged;
			}

			if (previewText)
				await GetPreviewTextAsync (ctx, doAsync, cancellationToken).ConfigureAwait (false);

			return AsReadOnly (ctx.Messages);
		}

		async Task<IList<IMessageSummary>> FetchAsync (IList<UniqueId> uids, ulong modseq, MessageSummaryItems items, bool doAsync, CancellationToken cancellationToken)
		{
			bool previewText;

			if (uids == null)
				throw new ArgumentNullException (nameof (uids));

			if (items == MessageSummaryItems.None)
				throw new ArgumentOutOfRangeException (nameof (items));

			if (!supportsModSeq)
				throw new NotSupportedException ("The ImapFolder does not support mod-sequences.");

			CheckState (true, false);

			if (uids.Count == 0)
				return new IMessageSummary[0];

			var query = FormatSummaryItems (ref items, EmptyHeaderFields, out previewText);
			var vanished = Engine.QResyncEnabled ? " VANISHED" : string.Empty;
			var modseqValue = modseq.ToString (CultureInfo.InvariantCulture);
			var command = string.Format ("UID FETCH %s {0} (CHANGEDSINCE {1}{2})\r\n", query, modseqValue, vanished);
			var ctx = new FetchSummaryContext ();

			MessageExpunged += ctx.OnMessageExpunged;

			try {
				foreach (var ic in Engine.CreateCommands (cancellationToken, this, command, uids)) {
					ic.RegisterUntaggedHandler ("FETCH", FetchSummaryItemsAsync);
					ic.UserData = ctx;

					Engine.QueueCommand (ic);

					await Engine.RunAsync (ic, doAsync).ConfigureAwait (false);

					ProcessResponseCodes (ic, null);

					if (ic.Response != ImapCommandResponse.Ok)
						throw ImapCommandException.Create ("FETCH", ic);
				}
			} finally {
				MessageExpunged -= ctx.OnMessageExpunged;
			}

			if (previewText)
				await GetPreviewTextAsync (ctx, doAsync, cancellationToken).ConfigureAwait (false);

			return AsReadOnly (ctx.Messages);
		}

		async Task<IList<IMessageSummary>> FetchAsync (IList<UniqueId> uids, ulong modseq, MessageSummaryItems items, IEnumerable<string> headers, bool doAsync, CancellationToken cancellationToken)
		{
			bool previewText;

			if (uids == null)
				throw new ArgumentNullException (nameof (uids));

			var headerFields = ImapUtils.GetUniqueHeaders (headers);

			if (!supportsModSeq)
				throw new NotSupportedException ("The ImapFolder does not support mod-sequences.");

			CheckState (true, false);

			if (uids.Count == 0)
				return new IMessageSummary[0];

			var query = FormatSummaryItems (ref items, headerFields, out previewText);
			var vanished = Engine.QResyncEnabled ? " VANISHED" : string.Empty;
			var modseqValue = modseq.ToString (CultureInfo.InvariantCulture);
			var command = string.Format ("UID FETCH %s {0} (CHANGEDSINCE {1}{2})\r\n", query, modseqValue, vanished);
			var ctx = new FetchSummaryContext ();

			MessageExpunged += ctx.OnMessageExpunged;

			try {
				foreach (var ic in Engine.CreateCommands (cancellationToken, this, command, uids)) {
					ic.RegisterUntaggedHandler ("FETCH", FetchSummaryItemsAsync);
					ic.UserData = ctx;

					Engine.QueueCommand (ic);

					await Engine.RunAsync (ic, doAsync).ConfigureAwait (false);

					ProcessResponseCodes (ic, null);

					if (ic.Response != ImapCommandResponse.Ok)
						throw ImapCommandException.Create ("FETCH", ic);
				}
			} finally {
				MessageExpunged -= ctx.OnMessageExpunged;
			}

			if (previewText)
				await GetPreviewTextAsync (ctx, doAsync, cancellationToken).ConfigureAwait (false);

			return AsReadOnly (ctx.Messages);
		}

		/// <summary>
		/// Fetches the message summaries for the specified message UIDs.
		/// </summary>
		/// <remarks>
		/// <para>Fetches the message summaries for the specified message UIDs.</para>
		/// <para>It should be noted that if another client has modified any message
		/// in the folder, the IMAP server may choose to return information that was
		/// not explicitly requested. It is therefore important to be prepared to
		/// handle both additional fields on a <see cref="IMessageSummary"/> for
		/// messages that were requested as well as summaries for messages that were
		/// not requested at all.</para>
		/// </remarks>
		/// <example>
		/// <code language="c#" source="Examples\ImapExamples.cs" region="DownloadBodyParts"/>
		/// </example>
		/// <returns>An enumeration of summaries for the requested messages.</returns>
		/// <param name="uids">The UIDs.</param>
		/// <param name="items">The message summary items to fetch.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <paramref name="uids"/> is <c>null</c>.
		/// </exception>
		/// <exception cref="System.ArgumentOutOfRangeException">
		/// <paramref name="items"/> is empty.
		/// </exception>
		/// <exception cref="System.ArgumentException">
		/// One or more of the <paramref name="uids"/> is invalid.
		/// </exception>
		/// <exception cref="System.ObjectDisposedException">
		/// The <see cref="ImapClient"/> has been disposed.
		/// </exception>
		/// <exception cref="FolderNotOpenException">
		/// The <see cref="ImapFolder"/> is not currently open.
		/// </exception>
		/// <exception cref="ServiceNotConnectedException">
		/// The <see cref="ImapClient"/> is not connected.
		/// </exception>
		/// <exception cref="ServiceNotAuthenticatedException">
		/// The <see cref="ImapClient"/> is not authenticated.
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
		public override IList<IMessageSummary> Fetch (IList<UniqueId> uids, MessageSummaryItems items, CancellationToken cancellationToken = default (CancellationToken))
		{
			return FetchAsync (uids, items, false, cancellationToken).GetAwaiter ().GetResult ();
		}

		/// <summary>
		/// Asynchronously fetches the message summaries for the specified message UIDs.
		/// </summary>
		/// <remarks>
		/// <para>Fetches the message summaries for the specified message UIDs.</para>
		/// <para>It should be noted that if another client has modified any message
		/// in the folder, the IMAP server may choose to return information that was
		/// not explicitly requested. It is therefore important to be prepared to
		/// handle both additional fields on a <see cref="IMessageSummary"/> for
		/// messages that were requested as well as summaries for messages that were
		/// not requested at all.</para>
		/// </remarks>
		/// <example>
		/// <code language="c#" source="Examples\ImapExamples.cs" region="DownloadBodyParts"/>
		/// </example>
		/// <returns>An enumeration of summaries for the requested messages.</returns>
		/// <param name="uids">The UIDs.</param>
		/// <param name="items">The message summary items to fetch.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <paramref name="uids"/> is <c>null</c>.
		/// </exception>
		/// <exception cref="System.ArgumentOutOfRangeException">
		/// <paramref name="items"/> is empty.
		/// </exception>
		/// <exception cref="System.ArgumentException">
		/// One or more of the <paramref name="uids"/> is invalid.
		/// </exception>
		/// <exception cref="System.ObjectDisposedException">
		/// The <see cref="ImapClient"/> has been disposed.
		/// </exception>
		/// <exception cref="FolderNotOpenException">
		/// The <see cref="ImapFolder"/> is not currently open.
		/// </exception>
		/// <exception cref="ServiceNotConnectedException">
		/// The <see cref="ImapClient"/> is not connected.
		/// </exception>
		/// <exception cref="ServiceNotAuthenticatedException">
		/// The <see cref="ImapClient"/> is not authenticated.
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
		public override Task<IList<IMessageSummary>> FetchAsync (IList<UniqueId> uids, MessageSummaryItems items, CancellationToken cancellationToken = default (CancellationToken))
		{
			return FetchAsync (uids, items, true, cancellationToken);
		}

		/// <summary>
		/// Fetches the message summaries for the specified message UIDs.
		/// </summary>
		/// <remarks>
		/// <para>Fetches the message summaries for the specified message UIDs.</para>
		/// <para>It should be noted that if another client has modified any message
		/// in the folder, the IMAP server may choose to return information that was
		/// not explicitly requested. It is therefore important to be prepared to
		/// handle both additional fields on a <see cref="IMessageSummary"/> for
		/// messages that were requested as well as summaries for messages that were
		/// not requested at all.</para>
		/// </remarks>
		/// <returns>An enumeration of summaries for the requested messages.</returns>
		/// <param name="uids">The UIDs.</param>
		/// <param name="items">The message summary items to fetch.</param>
		/// <param name="headers">The desired header fields.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <para><paramref name="uids"/> is <c>null</c>.</para>
		/// <para>-or-</para>
		/// <para><paramref name="headers"/> is <c>null</c>.</para>
		/// </exception>
		/// <exception cref="System.ArgumentException">
		/// One or more of the <paramref name="uids"/> is invalid.
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
		public override IList<IMessageSummary> Fetch (IList<UniqueId> uids, MessageSummaryItems items, IEnumerable<HeaderId> headers, CancellationToken cancellationToken = default (CancellationToken))
		{
			return Fetch (uids, items, ImapUtils.GetUniqueHeaders (headers), cancellationToken);
		}

		/// <summary>
		/// Asynchronously fetches the message summaries for the specified message UIDs.
		/// </summary>
		/// <remarks>
		/// <para>Fetches the message summaries for the specified message UIDs.</para>
		/// <para>It should be noted that if another client has modified any message
		/// in the folder, the IMAP server may choose to return information that was
		/// not explicitly requested. It is therefore important to be prepared to
		/// handle both additional fields on a <see cref="IMessageSummary"/> for
		/// messages that were requested as well as summaries for messages that were
		/// not requested at all.</para>
		/// </remarks>
		/// <returns>An enumeration of summaries for the requested messages.</returns>
		/// <param name="uids">The UIDs.</param>
		/// <param name="items">The message summary items to fetch.</param>
		/// <param name="headers">The desired header fields.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <para><paramref name="uids"/> is <c>null</c>.</para>
		/// <para>-or-</para>
		/// <para><paramref name="headers"/> is <c>null</c>.</para>
		/// </exception>
		/// <exception cref="System.ArgumentException">
		/// One or more of the <paramref name="uids"/> is invalid.
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
		public override Task<IList<IMessageSummary>> FetchAsync (IList<UniqueId> uids, MessageSummaryItems items, IEnumerable<HeaderId> headers, CancellationToken cancellationToken = default (CancellationToken))
		{
			return FetchAsync (uids, items, ImapUtils.GetUniqueHeaders (headers), cancellationToken);
		}

		/// <summary>
		/// Fetches the message summaries for the specified message UIDs.
		/// </summary>
		/// <remarks>
		/// <para>Fetches the message summaries for the specified message UIDs.</para>
		/// <para>It should be noted that if another client has modified any message
		/// in the folder, the IMAP server may choose to return information that was
		/// not explicitly requested. It is therefore important to be prepared to
		/// handle both additional fields on a <see cref="IMessageSummary"/> for
		/// messages that were requested as well as summaries for messages that were
		/// not requested at all.</para>
		/// </remarks>
		/// <returns>An enumeration of summaries for the requested messages.</returns>
		/// <param name="uids">The UIDs.</param>
		/// <param name="items">The message summary items to fetch.</param>
		/// <param name="headers">The desired header fields.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <para><paramref name="uids"/> is <c>null</c>.</para>
		/// <para>-or-</para>
		/// <para><paramref name="headers"/> is <c>null</c>.</para>
		/// </exception>
		/// <exception cref="System.ArgumentException">
		/// <para>One or more of the <paramref name="uids"/> is invalid.</para>
		/// <para>-or-</para>
		/// <para>One or more of the specified <paramref name="headers"/> is invalid.</para>
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
		public override IList<IMessageSummary> Fetch (IList<UniqueId> uids, MessageSummaryItems items, IEnumerable<string> headers, CancellationToken cancellationToken = default (CancellationToken))
		{
			return FetchAsync (uids, items, headers, false, cancellationToken).GetAwaiter ().GetResult ();
		}

		/// <summary>
		/// Asynchronously fetches the message summaries for the specified message UIDs.
		/// </summary>
		/// <remarks>
		/// <para>Fetches the message summaries for the specified message UIDs.</para>
		/// <para>It should be noted that if another client has modified any message
		/// in the folder, the IMAP server may choose to return information that was
		/// not explicitly requested. It is therefore important to be prepared to
		/// handle both additional fields on a <see cref="IMessageSummary"/> for
		/// messages that were requested as well as summaries for messages that were
		/// not requested at all.</para>
		/// </remarks>
		/// <returns>An enumeration of summaries for the requested messages.</returns>
		/// <param name="uids">The UIDs.</param>
		/// <param name="items">The message summary items to fetch.</param>
		/// <param name="headers">The desired header fields.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <para><paramref name="uids"/> is <c>null</c>.</para>
		/// <para>-or-</para>
		/// <para><paramref name="headers"/> is <c>null</c>.</para>
		/// </exception>
		/// <exception cref="System.ArgumentException">
		/// <para>One or more of the <paramref name="uids"/> is invalid.</para>
		/// <para>-or-</para>
		/// <para>One or more of the specified <paramref name="headers"/> is invalid.</para>
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
		public override Task<IList<IMessageSummary>> FetchAsync (IList<UniqueId> uids, MessageSummaryItems items, IEnumerable<string> headers, CancellationToken cancellationToken = default (CancellationToken))
		{
			return FetchAsync (uids, items, headers, true, cancellationToken);
		}

		/// <summary>
		/// Fetches the message summaries for the specified message UIDs that have a
		/// higher mod-sequence value than the one specified.
		/// </summary>
		/// <remarks>
		/// <para>Fetches the message summaries for the specified message UIDs that
		/// have a higher mod-sequence value than the one specified.</para>
		/// <para>If the IMAP server supports the QRESYNC extension and the application has
		/// enabled this feature via <see cref="ImapClient.EnableQuickResync(CancellationToken)"/>,
		/// then this method will emit <see cref="MailFolder.MessagesVanished"/> events for messages
		/// that have vanished since the specified mod-sequence value.</para>
		/// <para>It should be noted that if another client has modified any message
		/// in the folder, the IMAP server may choose to return information that was
		/// not explicitly requested. It is therefore important to be prepared to
		/// handle both additional fields on a <see cref="IMessageSummary"/> for
		/// messages that were requested as well as summaries for messages that were
		/// not requested at all.</para>
		/// </remarks>
		/// <returns>An enumeration of summaries for the requested messages.</returns>
		/// <param name="uids">The UIDs.</param>
		/// <param name="modseq">The mod-sequence value.</param>
		/// <param name="items">The message summary items to fetch.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <paramref name="uids"/> is <c>null</c>.
		/// </exception>
		/// <exception cref="System.ArgumentOutOfRangeException">
		/// <paramref name="items"/> is empty.
		/// </exception>
		/// <exception cref="System.ArgumentException">
		/// One or more of the <paramref name="uids"/> is invalid.
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
		/// <exception cref="System.NotSupportedException">
		/// The <see cref="ImapFolder"/> does not support mod-sequences.
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
		public override IList<IMessageSummary> Fetch (IList<UniqueId> uids, ulong modseq, MessageSummaryItems items, CancellationToken cancellationToken = default (CancellationToken))
		{
			return FetchAsync (uids, modseq, items, false, cancellationToken).GetAwaiter ().GetResult ();
		}

		/// <summary>
		/// Asynchronously fetches the message summaries for the specified message UIDs that have a
		/// higher mod-sequence value than the one specified.
		/// </summary>
		/// <remarks>
		/// <para>Fetches the message summaries for the specified message UIDs that
		/// have a higher mod-sequence value than the one specified.</para>
		/// <para>If the IMAP server supports the QRESYNC extension and the application has
		/// enabled this feature via <see cref="ImapClient.EnableQuickResync(CancellationToken)"/>,
		/// then this method will emit <see cref="MailFolder.MessagesVanished"/> events for messages
		/// that have vanished since the specified mod-sequence value.</para>
		/// <para>It should be noted that if another client has modified any message
		/// in the folder, the IMAP server may choose to return information that was
		/// not explicitly requested. It is therefore important to be prepared to
		/// handle both additional fields on a <see cref="IMessageSummary"/> for
		/// messages that were requested as well as summaries for messages that were
		/// not requested at all.</para>
		/// </remarks>
		/// <returns>An enumeration of summaries for the requested messages.</returns>
		/// <param name="uids">The UIDs.</param>
		/// <param name="modseq">The mod-sequence value.</param>
		/// <param name="items">The message summary items to fetch.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <paramref name="uids"/> is <c>null</c>.
		/// </exception>
		/// <exception cref="System.ArgumentOutOfRangeException">
		/// <paramref name="items"/> is empty.
		/// </exception>
		/// <exception cref="System.ArgumentException">
		/// One or more of the <paramref name="uids"/> is invalid.
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
		/// <exception cref="System.NotSupportedException">
		/// The <see cref="ImapFolder"/> does not support mod-sequences.
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
		public override Task<IList<IMessageSummary>> FetchAsync (IList<UniqueId> uids, ulong modseq, MessageSummaryItems items, CancellationToken cancellationToken = default (CancellationToken))
		{
			return FetchAsync (uids, modseq, items, true, cancellationToken);
		}

		/// <summary>
		/// Fetches the message summaries for the specified message UIDs that have a
		/// higher mod-sequence value than the one specified.
		/// </summary>
		/// <remarks>
		/// <para>Fetches the message summaries for the specified message UIDs that
		/// have a higher mod-sequence value than the one specified.</para>
		/// <para>If the IMAP server supports the QRESYNC extension and the application has
		/// enabled this feature via <see cref="ImapClient.EnableQuickResync(CancellationToken)"/>,
		/// then this method will emit <see cref="MailFolder.MessagesVanished"/> events for messages
		/// that have vanished since the specified mod-sequence value.</para>
		/// <para>It should be noted that if another client has modified any message
		/// in the folder, the IMAP server may choose to return information that was
		/// not explicitly requested. It is therefore important to be prepared to
		/// handle both additional fields on a <see cref="IMessageSummary"/> for
		/// messages that were requested as well as summaries for messages that were
		/// not requested at all.</para>
		/// </remarks>
		/// <returns>An enumeration of summaries for the requested messages.</returns>
		/// <param name="uids">The UIDs.</param>
		/// <param name="modseq">The mod-sequence value.</param>
		/// <param name="items">The message summary items to fetch.</param>
		/// <param name="headers">The desired header fields.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <para><paramref name="uids"/> is <c>null</c>.</para>
		/// <para>-or-</para>
		/// <para><paramref name="headers"/> is <c>null</c>.</para>
		/// </exception>
		/// <exception cref="System.ArgumentException">
		/// One or more of the <paramref name="uids"/> is invalid.
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
		/// <exception cref="System.NotSupportedException">
		/// The <see cref="ImapFolder"/> does not support mod-sequences.
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
		public override IList<IMessageSummary> Fetch (IList<UniqueId> uids, ulong modseq, MessageSummaryItems items, IEnumerable<HeaderId> headers, CancellationToken cancellationToken = default (CancellationToken))
		{
			return Fetch (uids, modseq, items, ImapUtils.GetUniqueHeaders (headers), cancellationToken);
		}

		/// <summary>
		/// Asynchronously fetches the message summaries for the specified message UIDs that have a
		/// higher mod-sequence value than the one specified.
		/// </summary>
		/// <remarks>
		/// <para>Fetches the message summaries for the specified message UIDs that
		/// have a higher mod-sequence value than the one specified.</para>
		/// <para>If the IMAP server supports the QRESYNC extension and the application has
		/// enabled this feature via <see cref="ImapClient.EnableQuickResync(CancellationToken)"/>,
		/// then this method will emit <see cref="MailFolder.MessagesVanished"/> events for messages
		/// that have vanished since the specified mod-sequence value.</para>
		/// <para>It should be noted that if another client has modified any message
		/// in the folder, the IMAP server may choose to return information that was
		/// not explicitly requested. It is therefore important to be prepared to
		/// handle both additional fields on a <see cref="IMessageSummary"/> for
		/// messages that were requested as well as summaries for messages that were
		/// not requested at all.</para>
		/// </remarks>
		/// <returns>An enumeration of summaries for the requested messages.</returns>
		/// <param name="uids">The UIDs.</param>
		/// <param name="modseq">The mod-sequence value.</param>
		/// <param name="items">The message summary items to fetch.</param>
		/// <param name="headers">The desired header fields.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <para><paramref name="uids"/> is <c>null</c>.</para>
		/// <para>-or-</para>
		/// <para><paramref name="headers"/> is <c>null</c>.</para>
		/// </exception>
		/// <exception cref="System.ArgumentException">
		/// One or more of the <paramref name="uids"/> is invalid.
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
		/// <exception cref="System.NotSupportedException">
		/// The <see cref="ImapFolder"/> does not support mod-sequences.
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
		public override Task<IList<IMessageSummary>> FetchAsync (IList<UniqueId> uids, ulong modseq, MessageSummaryItems items, IEnumerable<HeaderId> headers, CancellationToken cancellationToken = default (CancellationToken))
		{
			return FetchAsync (uids, modseq, items, ImapUtils.GetUniqueHeaders (headers), cancellationToken);
		}

		/// <summary>
		/// Fetches the message summaries for the specified message UIDs that have a
		/// higher mod-sequence value than the one specified.
		/// </summary>
		/// <remarks>
		/// <para>Fetches the message summaries for the specified message UIDs that
		/// have a higher mod-sequence value than the one specified.</para>
		/// <para>If the IMAP server supports the QRESYNC extension and the application has
		/// enabled this feature via <see cref="ImapClient.EnableQuickResync(CancellationToken)"/>,
		/// then this method will emit <see cref="MailFolder.MessagesVanished"/> events for messages
		/// that have vanished since the specified mod-sequence value.</para>
		/// <para>It should be noted that if another client has modified any message
		/// in the folder, the IMAP server may choose to return information that was
		/// not explicitly requested. It is therefore important to be prepared to
		/// handle both additional fields on a <see cref="IMessageSummary"/> for
		/// messages that were requested as well as summaries for messages that were
		/// not requested at all.</para>
		/// </remarks>
		/// <returns>An enumeration of summaries for the requested messages.</returns>
		/// <param name="uids">The UIDs.</param>
		/// <param name="modseq">The mod-sequence value.</param>
		/// <param name="items">The message summary items to fetch.</param>
		/// <param name="headers">The desired header fields.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <para><paramref name="uids"/> is <c>null</c>.</para>
		/// <para>-or-</para>
		/// <para><paramref name="headers"/> is <c>null</c>.</para>
		/// </exception>
		/// <exception cref="System.ArgumentException">
		/// <para>One or more of the <paramref name="uids"/> is invalid.</para>
		/// <para>-or-</para>
		/// <para>One or more of the specified <paramref name="headers"/> is invalid.</para>
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
		/// <exception cref="System.NotSupportedException">
		/// The <see cref="ImapFolder"/> does not support mod-sequences.
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
		public override IList<IMessageSummary> Fetch (IList<UniqueId> uids, ulong modseq, MessageSummaryItems items, IEnumerable<string> headers, CancellationToken cancellationToken = default (CancellationToken))
		{
			return FetchAsync (uids, modseq, items, headers, false, cancellationToken).GetAwaiter ().GetResult ();
		}

		/// <summary>
		/// Asynchronously fetches the message summaries for the specified message UIDs that have a
		/// higher mod-sequence value than the one specified.
		/// </summary>
		/// <remarks>
		/// <para>Fetches the message summaries for the specified message UIDs that
		/// have a higher mod-sequence value than the one specified.</para>
		/// <para>If the IMAP server supports the QRESYNC extension and the application has
		/// enabled this feature via <see cref="ImapClient.EnableQuickResync(CancellationToken)"/>,
		/// then this method will emit <see cref="MailFolder.MessagesVanished"/> events for messages
		/// that have vanished since the specified mod-sequence value.</para>
		/// <para>It should be noted that if another client has modified any message
		/// in the folder, the IMAP server may choose to return information that was
		/// not explicitly requested. It is therefore important to be prepared to
		/// handle both additional fields on a <see cref="IMessageSummary"/> for
		/// messages that were requested as well as summaries for messages that were
		/// not requested at all.</para>
		/// </remarks>
		/// <returns>An enumeration of summaries for the requested messages.</returns>
		/// <param name="uids">The UIDs.</param>
		/// <param name="modseq">The mod-sequence value.</param>
		/// <param name="items">The message summary items to fetch.</param>
		/// <param name="headers">The desired header fields.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <para><paramref name="uids"/> is <c>null</c>.</para>
		/// <para>-or-</para>
		/// <para><paramref name="headers"/> is <c>null</c>.</para>
		/// </exception>
		/// <exception cref="System.ArgumentException">
		/// <para>One or more of the <paramref name="uids"/> is invalid.</para>
		/// <para>-or-</para>
		/// <para>One or more of the specified <paramref name="headers"/> is invalid.</para>
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
		/// <exception cref="System.NotSupportedException">
		/// The <see cref="ImapFolder"/> does not support mod-sequences.
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
		public override Task<IList<IMessageSummary>> FetchAsync (IList<UniqueId> uids, ulong modseq, MessageSummaryItems items, IEnumerable<string> headers, CancellationToken cancellationToken = default (CancellationToken))
		{
			return FetchAsync (uids, modseq, items, headers, true, cancellationToken);
		}

		async Task<IList<IMessageSummary>> FetchAsync (IList<int> indexes, MessageSummaryItems items, bool doAsync, CancellationToken cancellationToken)
		{
			bool previewText;

			if (indexes == null)
				throw new ArgumentNullException (nameof (indexes));

			if (items == MessageSummaryItems.None)
				throw new ArgumentOutOfRangeException (nameof (items));

			CheckState (true, false);
			CheckAllowIndexes ();

			if (indexes.Count == 0)
				return new IMessageSummary[0];

			var set = ImapUtils.FormatIndexSet (indexes);
			var query = FormatSummaryItems (ref items, EmptyHeaderFields, out previewText);
			var command = string.Format ("FETCH {0} {1}\r\n", set, query);
			var ic = new ImapCommand (Engine, cancellationToken, this, command);
			var ctx = new FetchSummaryContext ();

			ic.RegisterUntaggedHandler ("FETCH", FetchSummaryItemsAsync);
			ic.UserData = ctx;

			Engine.QueueCommand (ic);

			await Engine.RunAsync (ic, doAsync).ConfigureAwait (false);

			ProcessResponseCodes (ic, null);

			if (ic.Response != ImapCommandResponse.Ok)
				throw ImapCommandException.Create ("FETCH", ic);

			if (previewText)
				await GetPreviewTextAsync (ctx, doAsync, cancellationToken).ConfigureAwait (false);

			return AsReadOnly (ctx.Messages);
		}

		async Task<IList<IMessageSummary>> FetchAsync (IList<int> indexes, MessageSummaryItems items, IEnumerable<string> headers, bool doAsync, CancellationToken cancellationToken)
		{
			bool previewText;

			if (indexes == null)
				throw new ArgumentNullException (nameof (indexes));

			var headerFields = ImapUtils.GetUniqueHeaders (headers);

			CheckState (true, false);
			CheckAllowIndexes ();

			if (indexes.Count == 0)
				return new IMessageSummary[0];

			var set = ImapUtils.FormatIndexSet (indexes);
			var query = FormatSummaryItems (ref items, headerFields, out previewText);
			var command = string.Format ("FETCH {0} {1}\r\n", set, query);
			var ic = new ImapCommand (Engine, cancellationToken, this, command);
			var ctx = new FetchSummaryContext ();

			ic.RegisterUntaggedHandler ("FETCH", FetchSummaryItemsAsync);
			ic.UserData = ctx;

			Engine.QueueCommand (ic);

			await Engine.RunAsync (ic, doAsync).ConfigureAwait (false);

			ProcessResponseCodes (ic, null);

			if (ic.Response != ImapCommandResponse.Ok)
				throw ImapCommandException.Create ("FETCH", ic);

			if (previewText)
				await GetPreviewTextAsync (ctx, doAsync, cancellationToken).ConfigureAwait (false);

			return AsReadOnly (ctx.Messages);
		}

		async Task<IList<IMessageSummary>> FetchAsync (IList<int> indexes, ulong modseq, MessageSummaryItems items, bool doAsync, CancellationToken cancellationToken)
		{
			bool previewText;

			if (indexes == null)
				throw new ArgumentNullException (nameof (indexes));

			if (items == MessageSummaryItems.None)
				throw new ArgumentOutOfRangeException (nameof (items));

			if (!supportsModSeq)
				throw new NotSupportedException ("The ImapFolder does not support mod-sequences.");

			CheckState (true, false);
			CheckAllowIndexes ();

			if (indexes.Count == 0)
				return new IMessageSummary[0];

			var set = ImapUtils.FormatIndexSet (indexes);
			var query = FormatSummaryItems (ref items, EmptyHeaderFields, out previewText);
			var modseqValue = modseq.ToString (CultureInfo.InvariantCulture);
			var command = string.Format ("FETCH {0} {1} (CHANGEDSINCE {2})\r\n", set, query, modseqValue);
			var ic = new ImapCommand (Engine, cancellationToken, this, command);
			var ctx = new FetchSummaryContext ();

			ic.RegisterUntaggedHandler ("FETCH", FetchSummaryItemsAsync);
			ic.UserData = ctx;

			Engine.QueueCommand (ic);

			await Engine.RunAsync (ic, doAsync).ConfigureAwait (false);

			ProcessResponseCodes (ic, null);

			if (ic.Response != ImapCommandResponse.Ok)
				throw ImapCommandException.Create ("FETCH", ic);

			if (previewText)
				await GetPreviewTextAsync (ctx, doAsync, cancellationToken).ConfigureAwait (false);

			return AsReadOnly (ctx.Messages);
		}

		async Task<IList<IMessageSummary>> FetchAsync (IList<int> indexes, ulong modseq, MessageSummaryItems items, IEnumerable<string> headers, bool doAsync, CancellationToken cancellationToken)
		{
			bool previewText;

			if (indexes == null)
				throw new ArgumentNullException (nameof (indexes));

			var headerFields = ImapUtils.GetUniqueHeaders (headers);

			if (!supportsModSeq)
				throw new NotSupportedException ("The ImapFolder does not support mod-sequences.");

			CheckState (true, false);
			CheckAllowIndexes ();

			if (indexes.Count == 0)
				return new IMessageSummary[0];

			var set = ImapUtils.FormatIndexSet (indexes);
			var query = FormatSummaryItems (ref items, headerFields, out previewText);
			var modseqValue = modseq.ToString (CultureInfo.InvariantCulture);
			var command = string.Format ("FETCH {0} {1} (CHANGEDSINCE {2})\r\n", set, query, modseqValue);
			var ic = new ImapCommand (Engine, cancellationToken, this, command);
			var ctx = new FetchSummaryContext ();

			ic.RegisterUntaggedHandler ("FETCH", FetchSummaryItemsAsync);
			ic.UserData = ctx;

			Engine.QueueCommand (ic);

			await Engine.RunAsync (ic, doAsync).ConfigureAwait (false);

			ProcessResponseCodes (ic, null);

			if (ic.Response != ImapCommandResponse.Ok)
				throw ImapCommandException.Create ("FETCH", ic);

			if (previewText)
				await GetPreviewTextAsync (ctx, doAsync, cancellationToken).ConfigureAwait (false);

			return AsReadOnly (ctx.Messages);
		}

		/// <summary>
		/// Fetches the message summaries for the specified message indexes.
		/// </summary>
		/// <remarks>
		/// <para>Fetches the message summaries for the specified message indexes.</para>
		/// <para>It should be noted that if another client has modified any message
		/// in the folder, the IMAP server may choose to return information that was
		/// not explicitly requested. It is therefore important to be prepared to
		/// handle both additional fields on a <see cref="IMessageSummary"/> for
		/// messages that were requested as well as summaries for messages that were
		/// not requested at all.</para>
		/// </remarks>
		/// <returns>An enumeration of summaries for the requested messages.</returns>
		/// <param name="indexes">The indexes.</param>
		/// <param name="items">The message summary items to fetch.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <paramref name="indexes"/> is <c>null</c>.
		/// </exception>
		/// <exception cref="System.ArgumentOutOfRangeException">
		/// <paramref name="items"/> is empty.
		/// </exception>
		/// <exception cref="System.ArgumentException">
		/// One or more of the <paramref name="indexes"/> is invalid.
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
		public override IList<IMessageSummary> Fetch (IList<int> indexes, MessageSummaryItems items, CancellationToken cancellationToken = default (CancellationToken))
		{
			return FetchAsync (indexes, items, false, cancellationToken).GetAwaiter ().GetResult ();
		}

		/// <summary>
		/// Asynchronously fetches the message summaries for the specified message indexes.
		/// </summary>
		/// <remarks>
		/// <para>Fetches the message summaries for the specified message indexes.</para>
		/// <para>It should be noted that if another client has modified any message
		/// in the folder, the IMAP server may choose to return information that was
		/// not explicitly requested. It is therefore important to be prepared to
		/// handle both additional fields on a <see cref="IMessageSummary"/> for
		/// messages that were requested as well as summaries for messages that were
		/// not requested at all.</para>
		/// </remarks>
		/// <returns>An enumeration of summaries for the requested messages.</returns>
		/// <param name="indexes">The indexes.</param>
		/// <param name="items">The message summary items to fetch.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <paramref name="indexes"/> is <c>null</c>.
		/// </exception>
		/// <exception cref="System.ArgumentOutOfRangeException">
		/// <paramref name="items"/> is empty.
		/// </exception>
		/// <exception cref="System.ArgumentException">
		/// One or more of the <paramref name="indexes"/> is invalid.
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
		public override Task<IList<IMessageSummary>> FetchAsync (IList<int> indexes, MessageSummaryItems items, CancellationToken cancellationToken = default (CancellationToken))
		{
			return FetchAsync (indexes, items, true, cancellationToken);
		}

		/// <summary>
		/// Fetches the message summaries for the specified message indexes.
		/// </summary>
		/// <remarks>
		/// <para>Fetches the message summaries for the specified message indexes.</para>
		/// <para>It should be noted that if another client has modified any message
		/// in the folder, the IMAP server may choose to return information that was
		/// not explicitly requested. It is therefore important to be prepared to
		/// handle both additional fields on a <see cref="IMessageSummary"/> for
		/// messages that were requested as well as summaries for messages that were
		/// not requested at all.</para>
		/// </remarks>
		/// <returns>An enumeration of summaries for the requested messages.</returns>
		/// <param name="indexes">The indexes.</param>
		/// <param name="items">The message summary items to fetch.</param>
		/// <param name="headers">The desired header fields.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <para><paramref name="indexes"/> is <c>null</c>.</para>
		/// <para>-or-</para>
		/// <para><paramref name="headers"/> is <c>null</c>.</para>
		/// </exception>
		/// <exception cref="System.ArgumentException">
		/// One or more of the <paramref name="indexes"/> is invalid.
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
		public override IList<IMessageSummary> Fetch (IList<int> indexes, MessageSummaryItems items, IEnumerable<HeaderId> headers, CancellationToken cancellationToken = default (CancellationToken))
		{
			return Fetch (indexes, items, ImapUtils.GetUniqueHeaders (headers), cancellationToken);
		}

		/// <summary>
		/// Asynchronously fetches the message summaries for the specified message indexes.
		/// </summary>
		/// <remarks>
		/// <para>Fetches the message summaries for the specified message indexes.</para>
		/// <para>It should be noted that if another client has modified any message
		/// in the folder, the IMAP server may choose to return information that was
		/// not explicitly requested. It is therefore important to be prepared to
		/// handle both additional fields on a <see cref="IMessageSummary"/> for
		/// messages that were requested as well as summaries for messages that were
		/// not requested at all.</para>
		/// </remarks>
		/// <returns>An enumeration of summaries for the requested messages.</returns>
		/// <param name="indexes">The indexes.</param>
		/// <param name="items">The message summary items to fetch.</param>
		/// <param name="headers">The desired header fields.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <para><paramref name="indexes"/> is <c>null</c>.</para>
		/// <para>-or-</para>
		/// <para><paramref name="headers"/> is <c>null</c>.</para>
		/// </exception>
		/// <exception cref="System.ArgumentException">
		/// One or more of the <paramref name="indexes"/> is invalid.
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
		public override Task<IList<IMessageSummary>> FetchAsync (IList<int> indexes, MessageSummaryItems items, IEnumerable<HeaderId> headers, CancellationToken cancellationToken = default (CancellationToken))
		{
			return FetchAsync (indexes, items, ImapUtils.GetUniqueHeaders (headers), cancellationToken);
		}

		/// <summary>
		/// Fetches the message summaries for the specified message indexes.
		/// </summary>
		/// <remarks>
		/// <para>Fetches the message summaries for the specified message indexes.</para>
		/// <para>It should be noted that if another client has modified any message
		/// in the folder, the IMAP server may choose to return information that was
		/// not explicitly requested. It is therefore important to be prepared to
		/// handle both additional fields on a <see cref="IMessageSummary"/> for
		/// messages that were requested as well as summaries for messages that were
		/// not requested at all.</para>
		/// </remarks>
		/// <returns>An enumeration of summaries for the requested messages.</returns>
		/// <param name="indexes">The indexes.</param>
		/// <param name="items">The message summary items to fetch.</param>
		/// <param name="headers">The desired header fields.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <para><paramref name="indexes"/> is <c>null</c>.</para>
		/// <para>-or-</para>
		/// <para><paramref name="headers"/> is <c>null</c>.</para>
		/// </exception>
		/// <exception cref="System.ArgumentException">
		/// <para>One or more of the <paramref name="indexes"/> is invalid.</para>
		/// <para>-or-</para>
		/// <para>One or more of the specified <paramref name="headers"/> is invalid.</para>
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
		public override IList<IMessageSummary> Fetch (IList<int> indexes, MessageSummaryItems items, IEnumerable<string> headers, CancellationToken cancellationToken = default (CancellationToken))
		{
			return FetchAsync (indexes, items, headers, false, cancellationToken).GetAwaiter ().GetResult ();
		}

		/// <summary>
		/// Asynchronously fetches the message summaries for the specified message indexes.
		/// </summary>
		/// <remarks>
		/// <para>Fetches the message summaries for the specified message indexes.</para>
		/// <para>It should be noted that if another client has modified any message
		/// in the folder, the IMAP server may choose to return information that was
		/// not explicitly requested. It is therefore important to be prepared to
		/// handle both additional fields on a <see cref="IMessageSummary"/> for
		/// messages that were requested as well as summaries for messages that were
		/// not requested at all.</para>
		/// </remarks>
		/// <returns>An enumeration of summaries for the requested messages.</returns>
		/// <param name="indexes">The indexes.</param>
		/// <param name="items">The message summary items to fetch.</param>
		/// <param name="headers">The desired header fields.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <para><paramref name="indexes"/> is <c>null</c>.</para>
		/// <para>-or-</para>
		/// <para><paramref name="headers"/> is <c>null</c>.</para>
		/// </exception>
		/// <exception cref="System.ArgumentException">
		/// <para>One or more of the <paramref name="indexes"/> is invalid.</para>
		/// <para>-or-</para>
		/// <para>One or more of the specified <paramref name="headers"/> is invalid.</para>
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
		public override Task<IList<IMessageSummary>> FetchAsync (IList<int> indexes, MessageSummaryItems items, IEnumerable<string> headers, CancellationToken cancellationToken = default (CancellationToken))
		{
			return FetchAsync (indexes, items, headers, true, cancellationToken);
		}

		/// <summary>
		/// Fetches the message summaries for the specified message indexes that have a
		/// higher mod-sequence value than the one specified.
		/// </summary>
		/// <remarks>
		/// <para>Fetches the message summaries for the specified message indexes that
		/// have a higher mod-sequence value than the one specified.</para>
		/// <para>It should be noted that if another client has modified any message
		/// in the folder, the IMAP server may choose to return information that was
		/// not explicitly requested. It is therefore important to be prepared to
		/// handle both additional fields on a <see cref="IMessageSummary"/> for
		/// messages that were requested as well as summaries for messages that were
		/// not requested at all.</para>
		/// </remarks>
		/// <returns>An enumeration of summaries for the requested messages.</returns>
		/// <param name="indexes">The indexes.</param>
		/// <param name="modseq">The mod-sequence value.</param>
		/// <param name="items">The message summary items to fetch.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <paramref name="indexes"/> is <c>null</c>.
		/// </exception>
		/// <exception cref="System.ArgumentOutOfRangeException">
		/// <paramref name="items"/> is empty.
		/// </exception>
		/// <exception cref="System.ArgumentException">
		/// One or more of the <paramref name="indexes"/> is invalid.
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
		/// <exception cref="System.NotSupportedException">
		/// The <see cref="ImapFolder"/> does not support mod-sequences.
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
		public override IList<IMessageSummary> Fetch (IList<int> indexes, ulong modseq, MessageSummaryItems items, CancellationToken cancellationToken = default (CancellationToken))
		{
			return FetchAsync (indexes, modseq, items, false, cancellationToken).GetAwaiter ().GetResult ();
		}

		/// <summary>
		/// Asynchronously fetches the message summaries for the specified message indexes that have a
		/// higher mod-sequence value than the one specified.
		/// </summary>
		/// <remarks>
		/// <para>Fetches the message summaries for the specified message indexes that
		/// have a higher mod-sequence value than the one specified.</para>
		/// <para>It should be noted that if another client has modified any message
		/// in the folder, the IMAP server may choose to return information that was
		/// not explicitly requested. It is therefore important to be prepared to
		/// handle both additional fields on a <see cref="IMessageSummary"/> for
		/// messages that were requested as well as summaries for messages that were
		/// not requested at all.</para>
		/// </remarks>
		/// <returns>An enumeration of summaries for the requested messages.</returns>
		/// <param name="indexes">The indexes.</param>
		/// <param name="modseq">The mod-sequence value.</param>
		/// <param name="items">The message summary items to fetch.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <paramref name="indexes"/> is <c>null</c>.
		/// </exception>
		/// <exception cref="System.ArgumentOutOfRangeException">
		/// <paramref name="items"/> is empty.
		/// </exception>
		/// <exception cref="System.ArgumentException">
		/// One or more of the <paramref name="indexes"/> is invalid.
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
		/// <exception cref="System.NotSupportedException">
		/// The <see cref="ImapFolder"/> does not support mod-sequences.
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
		public override Task<IList<IMessageSummary>> FetchAsync (IList<int> indexes, ulong modseq, MessageSummaryItems items, CancellationToken cancellationToken = default (CancellationToken))
		{
			return FetchAsync (indexes, modseq, items, true, cancellationToken);
		}

		/// <summary>
		/// Fetches the message summaries for the specified message indexes that have a
		/// higher mod-sequence value than the one specified.
		/// </summary>
		/// <remarks>
		/// <para>Fetches the message summaries for the specified message indexes that
		/// have a higher mod-sequence value than the one specified.</para>
		/// <para>It should be noted that if another client has modified any message
		/// in the folder, the IMAP server may choose to return information that was
		/// not explicitly requested. It is therefore important to be prepared to
		/// handle both additional fields on a <see cref="IMessageSummary"/> for
		/// messages that were requested as well as summaries for messages that were
		/// not requested at all.</para>
		/// </remarks>
		/// <returns>An enumeration of summaries for the requested messages.</returns>
		/// <param name="indexes">The indexes.</param>
		/// <param name="modseq">The mod-sequence value.</param>
		/// <param name="items">The message summary items to fetch.</param>
		/// <param name="headers">The desired header fields.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <para><paramref name="indexes"/> is <c>null</c>.</para>
		/// <para>-or-</para>
		/// <para><paramref name="headers"/> is <c>null</c>.</para>
		/// </exception>
		/// <exception cref="System.ArgumentException">
		/// One or more of the <paramref name="indexes"/> is invalid.
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
		/// <exception cref="System.NotSupportedException">
		/// The <see cref="ImapFolder"/> does not support mod-sequences.
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
		public override IList<IMessageSummary> Fetch (IList<int> indexes, ulong modseq, MessageSummaryItems items, IEnumerable<HeaderId> headers, CancellationToken cancellationToken = default (CancellationToken))
		{
			return Fetch (indexes, modseq, items, ImapUtils.GetUniqueHeaders (headers), cancellationToken);
		}

		/// <summary>
		/// Asynchronously fetches the message summaries for the specified message indexes that have a
		/// higher mod-sequence value than the one specified.
		/// </summary>
		/// <remarks>
		/// <para>Fetches the message summaries for the specified message indexes that
		/// have a higher mod-sequence value than the one specified.</para>
		/// <para>It should be noted that if another client has modified any message
		/// in the folder, the IMAP server may choose to return information that was
		/// not explicitly requested. It is therefore important to be prepared to
		/// handle both additional fields on a <see cref="IMessageSummary"/> for
		/// messages that were requested as well as summaries for messages that were
		/// not requested at all.</para>
		/// </remarks>
		/// <returns>An enumeration of summaries for the requested messages.</returns>
		/// <param name="indexes">The indexes.</param>
		/// <param name="modseq">The mod-sequence value.</param>
		/// <param name="items">The message summary items to fetch.</param>
		/// <param name="headers">The desired header fields.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <para><paramref name="indexes"/> is <c>null</c>.</para>
		/// <para>-or-</para>
		/// <para><paramref name="headers"/> is <c>null</c>.</para>
		/// </exception>
		/// <exception cref="System.ArgumentException">
		/// One or more of the <paramref name="indexes"/> is invalid.
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
		/// <exception cref="System.NotSupportedException">
		/// The <see cref="ImapFolder"/> does not support mod-sequences.
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
		public override Task<IList<IMessageSummary>> FetchAsync (IList<int> indexes, ulong modseq, MessageSummaryItems items, IEnumerable<HeaderId> headers, CancellationToken cancellationToken = default (CancellationToken))
		{
			return FetchAsync (indexes, modseq, items, ImapUtils.GetUniqueHeaders (headers), cancellationToken);
		}

		/// <summary>
		/// Fetches the message summaries for the specified message indexes that have a
		/// higher mod-sequence value than the one specified.
		/// </summary>
		/// <remarks>
		/// <para>Fetches the message summaries for the specified message indexes that
		/// have a higher mod-sequence value than the one specified.</para>
		/// <para>It should be noted that if another client has modified any message
		/// in the folder, the IMAP server may choose to return information that was
		/// not explicitly requested. It is therefore important to be prepared to
		/// handle both additional fields on a <see cref="IMessageSummary"/> for
		/// messages that were requested as well as summaries for messages that were
		/// not requested at all.</para>
		/// </remarks>
		/// <returns>An enumeration of summaries for the requested messages.</returns>
		/// <param name="indexes">The indexes.</param>
		/// <param name="modseq">The mod-sequence value.</param>
		/// <param name="items">The message summary items to fetch.</param>
		/// <param name="headers">The desired header fields.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <para><paramref name="indexes"/> is <c>null</c>.</para>
		/// <para>-or-</para>
		/// <para><paramref name="headers"/> is <c>null</c>.</para>
		/// </exception>
		/// <exception cref="System.ArgumentException">
		/// <para>One or more of the <paramref name="indexes"/> is invalid.</para>
		/// <para>-or-</para>
		/// <para>One or more of the specified <paramref name="headers"/> is invalid.</para>
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
		/// <exception cref="System.NotSupportedException">
		/// The <see cref="ImapFolder"/> does not support mod-sequences.
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
		public override IList<IMessageSummary> Fetch (IList<int> indexes, ulong modseq, MessageSummaryItems items, IEnumerable<string> headers, CancellationToken cancellationToken = default (CancellationToken))
		{
			return FetchAsync (indexes, modseq, items, headers, false, cancellationToken).GetAwaiter ().GetResult ();
		}

		/// <summary>
		/// Asynchronously fetches the message summaries for the specified message indexes that have a
		/// higher mod-sequence value than the one specified.
		/// </summary>
		/// <remarks>
		/// <para>Fetches the message summaries for the specified message indexes that
		/// have a higher mod-sequence value than the one specified.</para>
		/// <para>It should be noted that if another client has modified any message
		/// in the folder, the IMAP server may choose to return information that was
		/// not explicitly requested. It is therefore important to be prepared to
		/// handle both additional fields on a <see cref="IMessageSummary"/> for
		/// messages that were requested as well as summaries for messages that were
		/// not requested at all.</para>
		/// </remarks>
		/// <returns>An enumeration of summaries for the requested messages.</returns>
		/// <param name="indexes">The indexes.</param>
		/// <param name="modseq">The mod-sequence value.</param>
		/// <param name="items">The message summary items to fetch.</param>
		/// <param name="headers">The desired header fields.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <para><paramref name="indexes"/> is <c>null</c>.</para>
		/// <para>-or-</para>
		/// <para><paramref name="headers"/> is <c>null</c>.</para>
		/// </exception>
		/// <exception cref="System.ArgumentException">
		/// <para>One or more of the <paramref name="indexes"/> is invalid.</para>
		/// <para>-or-</para>
		/// <para>One or more of the specified <paramref name="headers"/> is invalid.</para>
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
		/// <exception cref="System.NotSupportedException">
		/// The <see cref="ImapFolder"/> does not support mod-sequences.
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
		public override Task<IList<IMessageSummary>> FetchAsync (IList<int> indexes, ulong modseq, MessageSummaryItems items, IEnumerable<string> headers, CancellationToken cancellationToken = default (CancellationToken))
		{
			return FetchAsync (indexes, modseq, items, headers, true, cancellationToken);
		}

		static string GetFetchRange (int min, int max)
		{
			var minValue = (min + 1).ToString (CultureInfo.InvariantCulture);

			if (min == max)
				return minValue;

			var maxValue = max != -1 ? (max + 1).ToString (CultureInfo.InvariantCulture) : "*";

			return string.Format (CultureInfo.InvariantCulture, "{0}:{1}", minValue, maxValue);
		}

		async Task<IList<IMessageSummary>> FetchAsync (int min, int max, MessageSummaryItems items, bool doAsync, CancellationToken cancellationToken)
		{
			bool previewText;

			if (min < 0)
				throw new ArgumentOutOfRangeException (nameof (min));

			if (max != -1 && max < min)
				throw new ArgumentOutOfRangeException (nameof (max));

			if (items == MessageSummaryItems.None)
				throw new ArgumentOutOfRangeException (nameof (items));

			CheckState (true, false);
			CheckAllowIndexes ();

			if (min == Count)
				return new IMessageSummary[0];

			var query = FormatSummaryItems (ref items, EmptyHeaderFields, out previewText);
			var command = string.Format ("FETCH {0} {1}\r\n", GetFetchRange (min, max), query);
			var ic = new ImapCommand (Engine, cancellationToken, this, command);
			var ctx = new FetchSummaryContext ();

			ic.RegisterUntaggedHandler ("FETCH", FetchSummaryItemsAsync);
			ic.UserData = ctx;

			Engine.QueueCommand (ic);

			await Engine.RunAsync (ic, doAsync).ConfigureAwait (false);

			ProcessResponseCodes (ic, null);

			if (ic.Response != ImapCommandResponse.Ok)
				throw ImapCommandException.Create ("FETCH", ic);

			if (previewText)
				await GetPreviewTextAsync (ctx, doAsync, cancellationToken).ConfigureAwait (false);

			return AsReadOnly (ctx.Messages);
		}

		async Task<IList<IMessageSummary>> FetchAsync (int min, int max, MessageSummaryItems items, IEnumerable<string> headers, bool doAsync, CancellationToken cancellationToken)
		{
			bool previewText;

			if (min < 0)
				throw new ArgumentOutOfRangeException (nameof (min));

			if (max != -1 && max < min)
				throw new ArgumentOutOfRangeException (nameof (max));

			var headerFields = ImapUtils.GetUniqueHeaders (headers);

			CheckState (true, false);
			CheckAllowIndexes ();

			if (min == Count)
				return new IMessageSummary[0];

			var query = FormatSummaryItems (ref items, headerFields, out previewText);
			var command = string.Format ("FETCH {0} {1}\r\n", GetFetchRange (min, max), query);
			var ic = new ImapCommand (Engine, cancellationToken, this, command);
			var ctx = new FetchSummaryContext ();

			ic.RegisterUntaggedHandler ("FETCH", FetchSummaryItemsAsync);
			ic.UserData = ctx;

			Engine.QueueCommand (ic);

			await Engine.RunAsync (ic, doAsync).ConfigureAwait (false);

			ProcessResponseCodes (ic, null);

			if (ic.Response != ImapCommandResponse.Ok)
				throw ImapCommandException.Create ("FETCH", ic);

			if (previewText)
				await GetPreviewTextAsync (ctx, doAsync, cancellationToken).ConfigureAwait (false);

			return AsReadOnly (ctx.Messages);
		}

		async Task<IList<IMessageSummary>> FetchAsync (int min, int max, ulong modseq, MessageSummaryItems items, bool doAsync, CancellationToken cancellationToken)
		{
			bool previewText;

			if (min < 0)
				throw new ArgumentOutOfRangeException (nameof (min));

			if (max != -1 && max < min)
				throw new ArgumentOutOfRangeException (nameof (max));

			if (items == MessageSummaryItems.None)
				throw new ArgumentOutOfRangeException (nameof (items));

			if (!supportsModSeq)
				throw new NotSupportedException ("The ImapFolder does not support mod-sequences.");

			CheckState (true, false);
			CheckAllowIndexes ();

			var query = FormatSummaryItems (ref items, EmptyHeaderFields, out previewText);
			var modseqValue = modseq.ToString (CultureInfo.InvariantCulture);
			var command = string.Format ("FETCH {0} {1} (CHANGEDSINCE {2})\r\n", GetFetchRange (min, max), query, modseqValue);
			var ic = new ImapCommand (Engine, cancellationToken, this, command);
			var ctx = new FetchSummaryContext ();

			ic.RegisterUntaggedHandler ("FETCH", FetchSummaryItemsAsync);
			ic.UserData = ctx;

			Engine.QueueCommand (ic);

			await Engine.RunAsync (ic, doAsync).ConfigureAwait (false);

			ProcessResponseCodes (ic, null);

			if (ic.Response != ImapCommandResponse.Ok)
				throw ImapCommandException.Create ("FETCH", ic);

			if (previewText)
				await GetPreviewTextAsync (ctx, doAsync, cancellationToken).ConfigureAwait (false);

			return AsReadOnly (ctx.Messages);
		}

		async Task<IList<IMessageSummary>> FetchAsync (int min, int max, ulong modseq, MessageSummaryItems items, IEnumerable<string> headers, bool doAsync, CancellationToken cancellationToken)
		{
			bool previewText;

			if (min < 0)
				throw new ArgumentOutOfRangeException (nameof (min));

			if (max != -1 && max < min)
				throw new ArgumentOutOfRangeException (nameof (max));

			var headerFields = ImapUtils.GetUniqueHeaders (headers);

			if (!supportsModSeq)
				throw new NotSupportedException ("The ImapFolder does not support mod-sequences.");

			CheckState (true, false);
			CheckAllowIndexes ();

			var query = FormatSummaryItems (ref items, headerFields, out previewText);
			var modseqValue = modseq.ToString (CultureInfo.InvariantCulture);
			var command = string.Format ("FETCH {0} {1} (CHANGEDSINCE {2})\r\n", GetFetchRange (min, max), query, modseqValue);
			var ic = new ImapCommand (Engine, cancellationToken, this, command);
			var ctx = new FetchSummaryContext ();

			ic.RegisterUntaggedHandler ("FETCH", FetchSummaryItemsAsync);
			ic.UserData = ctx;

			Engine.QueueCommand (ic);

			await Engine.RunAsync (ic, doAsync).ConfigureAwait (false);

			ProcessResponseCodes (ic, null);

			if (ic.Response != ImapCommandResponse.Ok)
				throw ImapCommandException.Create ("FETCH", ic);

			if (previewText)
				await GetPreviewTextAsync (ctx, doAsync, cancellationToken).ConfigureAwait (false);

			return AsReadOnly (ctx.Messages);
		}

		/// <summary>
		/// Fetches the message summaries for the messages between the two indexes, inclusive.
		/// </summary>
		/// <remarks>
		/// <para>Fetches the message summaries for the messages between the two
		/// indexes, inclusive.</para>
		/// <para>It should be noted that if another client has modified any message
		/// in the folder, the IMAP server may choose to return information that was
		/// not explicitly requested. It is therefore important to be prepared to
		/// handle both additional fields on a <see cref="IMessageSummary"/> for
		/// messages that were requested as well as summaries for messages that were
		/// not requested at all.</para>
		/// </remarks>
		/// <returns>An enumeration of summaries for the requested messages.</returns>
		/// <param name="min">The minimum index.</param>
		/// <param name="max">The maximum index, or <c>-1</c> to specify no upper bound.</param>
		/// <param name="items">The message summary items to fetch.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.ArgumentOutOfRangeException">
		/// <para><paramref name="min"/> is out of range.</para>
		/// <para>-or-</para>
		/// <para><paramref name="max"/> is out of range.</para>
		/// <para>-or-</para>
		/// <para><paramref name="items"/> is empty.</para>
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
		public override IList<IMessageSummary> Fetch (int min, int max, MessageSummaryItems items, CancellationToken cancellationToken = default (CancellationToken))
		{
			return FetchAsync (min, max, items, false, cancellationToken).GetAwaiter ().GetResult ();
		}

		/// <summary>
		/// Asynchronously fetches the message summaries for the messages between the two indexes, inclusive.
		/// </summary>
		/// <remarks>
		/// <para>Fetches the message summaries for the messages between the two
		/// indexes, inclusive.</para>
		/// <para>It should be noted that if another client has modified any message
		/// in the folder, the IMAP server may choose to return information that was
		/// not explicitly requested. It is therefore important to be prepared to
		/// handle both additional fields on a <see cref="IMessageSummary"/> for
		/// messages that were requested as well as summaries for messages that were
		/// not requested at all.</para>
		/// </remarks>
		/// <returns>An enumeration of summaries for the requested messages.</returns>
		/// <param name="min">The minimum index.</param>
		/// <param name="max">The maximum index, or <c>-1</c> to specify no upper bound.</param>
		/// <param name="items">The message summary items to fetch.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.ArgumentOutOfRangeException">
		/// <para><paramref name="min"/> is out of range.</para>
		/// <para>-or-</para>
		/// <para><paramref name="max"/> is out of range.</para>
		/// <para>-or-</para>
		/// <para><paramref name="items"/> is empty.</para>
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
		public override Task<IList<IMessageSummary>> FetchAsync (int min, int max, MessageSummaryItems items, CancellationToken cancellationToken = default (CancellationToken))
		{
			return FetchAsync (min, max, items, true, cancellationToken);
		}

		/// <summary>
		/// Fetches the message summaries for the messages between the two indexes, inclusive.
		/// </summary>
		/// <remarks>
		/// <para>Fetches the message summaries for the messages between the two
		/// indexes, inclusive.</para>
		/// <para>It should be noted that if another client has modified any message
		/// in the folder, the IMAP server may choose to return information that was
		/// not explicitly requested. It is therefore important to be prepared to
		/// handle both additional fields on a <see cref="IMessageSummary"/> for
		/// messages that were requested as well as summaries for messages that were
		/// not requested at all.</para>
		/// </remarks>
		/// <returns>An enumeration of summaries for the requested messages.</returns>
		/// <param name="min">The minimum index.</param>
		/// <param name="max">The maximum index, or <c>-1</c> to specify no upper bound.</param>
		/// <param name="items">The message summary items to fetch.</param>
		/// <param name="headers">The desired header fields.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.ArgumentOutOfRangeException">
		/// <para><paramref name="min"/> is out of range.</para>
		/// <para>-or-</para>
		/// <para><paramref name="max"/> is out of range.</para>
		/// </exception>
		/// <exception cref="System.ArgumentNullException">
		/// <paramref name="headers"/> is <c>null</c>.
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
		public override IList<IMessageSummary> Fetch (int min, int max, MessageSummaryItems items, IEnumerable<HeaderId> headers, CancellationToken cancellationToken = default (CancellationToken))
		{
			return Fetch (min, max, items, ImapUtils.GetUniqueHeaders (headers), cancellationToken);
		}

		/// <summary>
		/// Asynchronously fetches the message summaries for the messages between the two indexes, inclusive.
		/// </summary>
		/// <remarks>
		/// <para>Fetches the message summaries for the messages between the two
		/// indexes, inclusive.</para>
		/// <para>It should be noted that if another client has modified any message
		/// in the folder, the IMAP server may choose to return information that was
		/// not explicitly requested. It is therefore important to be prepared to
		/// handle both additional fields on a <see cref="IMessageSummary"/> for
		/// messages that were requested as well as summaries for messages that were
		/// not requested at all.</para>
		/// </remarks>
		/// <returns>An enumeration of summaries for the requested messages.</returns>
		/// <param name="min">The minimum index.</param>
		/// <param name="max">The maximum index, or <c>-1</c> to specify no upper bound.</param>
		/// <param name="items">The message summary items to fetch.</param>
		/// <param name="headers">The desired header fields.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.ArgumentOutOfRangeException">
		/// <para><paramref name="min"/> is out of range.</para>
		/// <para>-or-</para>
		/// <para><paramref name="max"/> is out of range.</para>
		/// </exception>
		/// <exception cref="System.ArgumentNullException">
		/// <paramref name="headers"/> is <c>null</c>.
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
		public override Task<IList<IMessageSummary>> FetchAsync (int min, int max, MessageSummaryItems items, IEnumerable<HeaderId> headers, CancellationToken cancellationToken = default (CancellationToken))
		{
			return FetchAsync (min, max, items, ImapUtils.GetUniqueHeaders (headers), cancellationToken);
		}

		/// <summary>
		/// Fetches the message summaries for the messages between the two indexes, inclusive.
		/// </summary>
		/// <remarks>
		/// <para>Fetches the message summaries for the messages between the two
		/// indexes, inclusive.</para>
		/// <para>It should be noted that if another client has modified any message
		/// in the folder, the IMAP server may choose to return information that was
		/// not explicitly requested. It is therefore important to be prepared to
		/// handle both additional fields on a <see cref="IMessageSummary"/> for
		/// messages that were requested as well as summaries for messages that were
		/// not requested at all.</para>
		/// </remarks>
		/// <returns>An enumeration of summaries for the requested messages.</returns>
		/// <param name="min">The minimum index.</param>
		/// <param name="max">The maximum index, or <c>-1</c> to specify no upper bound.</param>
		/// <param name="items">The message summary items to fetch.</param>
		/// <param name="headers">The desired header fields.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.ArgumentOutOfRangeException">
		/// <para><paramref name="min"/> is out of range.</para>
		/// <para>-or-</para>
		/// <para><paramref name="max"/> is out of range.</para>
		/// </exception>
		/// <exception cref="System.ArgumentNullException">
		/// <paramref name="headers"/> is <c>null</c>.
		/// </exception>
		/// <exception cref="System.ArgumentException">
		/// One or more of the specified <paramref name="headers"/> is invalid.
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
		public override IList<IMessageSummary> Fetch (int min, int max, MessageSummaryItems items, IEnumerable<string> headers, CancellationToken cancellationToken = default (CancellationToken))
		{
			return FetchAsync (min, max, items, headers, false, cancellationToken).GetAwaiter ().GetResult ();
		}

		/// <summary>
		/// Asynchronously fetches the message summaries for the messages between the two indexes, inclusive.
		/// </summary>
		/// <remarks>
		/// <para>Fetches the message summaries for the messages between the two
		/// indexes, inclusive.</para>
		/// <para>It should be noted that if another client has modified any message
		/// in the folder, the IMAP server may choose to return information that was
		/// not explicitly requested. It is therefore important to be prepared to
		/// handle both additional fields on a <see cref="IMessageSummary"/> for
		/// messages that were requested as well as summaries for messages that were
		/// not requested at all.</para>
		/// </remarks>
		/// <returns>An enumeration of summaries for the requested messages.</returns>
		/// <param name="min">The minimum index.</param>
		/// <param name="max">The maximum index, or <c>-1</c> to specify no upper bound.</param>
		/// <param name="items">The message summary items to fetch.</param>
		/// <param name="headers">The desired header fields.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.ArgumentOutOfRangeException">
		/// <para><paramref name="min"/> is out of range.</para>
		/// <para>-or-</para>
		/// <para><paramref name="max"/> is out of range.</para>
		/// </exception>
		/// <exception cref="System.ArgumentNullException">
		/// <paramref name="headers"/> is <c>null</c>.
		/// </exception>
		/// <exception cref="System.ArgumentException">
		/// One or more of the specified <paramref name="headers"/> is invalid.
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
		public override Task<IList<IMessageSummary>> FetchAsync (int min, int max, MessageSummaryItems items, IEnumerable<string> headers, CancellationToken cancellationToken = default (CancellationToken))
		{
			return FetchAsync (min, max, items, headers, true, cancellationToken);
		}

		/// <summary>
		/// Fetches the message summaries for the messages between the two indexes (inclusive)
		/// that have a higher mod-sequence value than the one specified.
		/// </summary>
		/// <remarks>
		/// <para>Fetches the message summaries for the messages between the two
		/// indexes (inclusive) that have a higher mod-sequence value than the one
		/// specified.</para>
		/// <para>It should be noted that if another client has modified any message
		/// in the folder, the IMAP server may choose to return information that was
		/// not explicitly requested. It is therefore important to be prepared to
		/// handle both additional fields on a <see cref="IMessageSummary"/> for
		/// messages that were requested as well as summaries for messages that were
		/// not requested at all.</para>
		/// </remarks>
		/// <returns>An enumeration of summaries for the requested messages.</returns>
		/// <param name="min">The minimum index.</param>
		/// <param name="max">The maximum index, or <c>-1</c> to specify no upper bound.</param>
		/// <param name="modseq">The mod-sequence value.</param>
		/// <param name="items">The message summary items to fetch.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.ArgumentOutOfRangeException">
		/// <para><paramref name="min"/> is out of range.</para>
		/// <para>-or-</para>
		/// <para><paramref name="max"/> is out of range.</para>
		/// <para>-or-</para>
		/// <para><paramref name="items"/> is empty.</para>
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
		/// <exception cref="System.NotSupportedException">
		/// The <see cref="ImapFolder"/> does not support mod-sequences.
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
		public override IList<IMessageSummary> Fetch (int min, int max, ulong modseq, MessageSummaryItems items, CancellationToken cancellationToken = default (CancellationToken))
		{
			return FetchAsync (min, max, modseq, items, false, cancellationToken).GetAwaiter ().GetResult ();
		}

		/// <summary>
		/// Asynchronously fetches the message summaries for the messages between the two indexes (inclusive)
		/// that have a higher mod-sequence value than the one specified.
		/// </summary>
		/// <remarks>
		/// <para>Fetches the message summaries for the messages between the two
		/// indexes (inclusive) that have a higher mod-sequence value than the one
		/// specified.</para>
		/// <para>It should be noted that if another client has modified any message
		/// in the folder, the IMAP server may choose to return information that was
		/// not explicitly requested. It is therefore important to be prepared to
		/// handle both additional fields on a <see cref="IMessageSummary"/> for
		/// messages that were requested as well as summaries for messages that were
		/// not requested at all.</para>
		/// </remarks>
		/// <returns>An enumeration of summaries for the requested messages.</returns>
		/// <param name="min">The minimum index.</param>
		/// <param name="max">The maximum index, or <c>-1</c> to specify no upper bound.</param>
		/// <param name="modseq">The mod-sequence value.</param>
		/// <param name="items">The message summary items to fetch.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.ArgumentOutOfRangeException">
		/// <para><paramref name="min"/> is out of range.</para>
		/// <para>-or-</para>
		/// <para><paramref name="max"/> is out of range.</para>
		/// <para>-or-</para>
		/// <para><paramref name="items"/> is empty.</para>
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
		/// <exception cref="System.NotSupportedException">
		/// The <see cref="ImapFolder"/> does not support mod-sequences.
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
		public override Task<IList<IMessageSummary>> FetchAsync (int min, int max, ulong modseq, MessageSummaryItems items, CancellationToken cancellationToken = default (CancellationToken))
		{
			return FetchAsync (min, max, modseq, items, true, cancellationToken);
		}

		/// <summary>
		/// Fetches the message summaries for the messages between the two indexes (inclusive)
		/// that have a higher mod-sequence value than the one specified.
		/// </summary>
		/// <remarks>
		/// <para>Fetches the message summaries for the messages between the two
		/// indexes (inclusive) that have a higher mod-sequence value than the one
		/// specified.</para>
		/// <para>It should be noted that if another client has modified any message
		/// in the folder, the IMAP server may choose to return information that was
		/// not explicitly requested. It is therefore important to be prepared to
		/// handle both additional fields on a <see cref="IMessageSummary"/> for
		/// messages that were requested as well as summaries for messages that were
		/// not requested at all.</para>
		/// </remarks>
		/// <returns>An enumeration of summaries for the requested messages.</returns>
		/// <param name="min">The minimum index.</param>
		/// <param name="max">The maximum index, or <c>-1</c> to specify no upper bound.</param>
		/// <param name="modseq">The mod-sequence value.</param>
		/// <param name="items">The message summary items to fetch.</param>
		/// <param name="headers">The desired header fields.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.ArgumentOutOfRangeException">
		/// <para><paramref name="min"/> is out of range.</para>
		/// <para>-or-</para>
		/// <para><paramref name="max"/> is out of range.</para>
		/// </exception>
		/// <exception cref="System.ArgumentNullException">
		/// <paramref name="headers"/> is <c>null</c>.
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
		/// <exception cref="System.NotSupportedException">
		/// The <see cref="ImapFolder"/> does not support mod-sequences.
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
		public override IList<IMessageSummary> Fetch (int min, int max, ulong modseq, MessageSummaryItems items, IEnumerable<HeaderId> headers, CancellationToken cancellationToken = default (CancellationToken))
		{
			return Fetch (min, max, modseq, items, ImapUtils.GetUniqueHeaders (headers), cancellationToken);
		}

		/// <summary>
		/// Asynchronously fetches the message summaries for the messages between the two indexes (inclusive)
		/// that have a higher mod-sequence value than the one specified.
		/// </summary>
		/// <remarks>
		/// <para>Fetches the message summaries for the messages between the two
		/// indexes (inclusive) that have a higher mod-sequence value than the one
		/// specified.</para>
		/// <para>It should be noted that if another client has modified any message
		/// in the folder, the IMAP server may choose to return information that was
		/// not explicitly requested. It is therefore important to be prepared to
		/// handle both additional fields on a <see cref="IMessageSummary"/> for
		/// messages that were requested as well as summaries for messages that were
		/// not requested at all.</para>
		/// </remarks>
		/// <returns>An enumeration of summaries for the requested messages.</returns>
		/// <param name="min">The minimum index.</param>
		/// <param name="max">The maximum index, or <c>-1</c> to specify no upper bound.</param>
		/// <param name="modseq">The mod-sequence value.</param>
		/// <param name="items">The message summary items to fetch.</param>
		/// <param name="headers">The desired header fields.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.ArgumentOutOfRangeException">
		/// <para><paramref name="min"/> is out of range.</para>
		/// <para>-or-</para>
		/// <para><paramref name="max"/> is out of range.</para>
		/// </exception>
		/// <exception cref="System.ArgumentNullException">
		/// <paramref name="headers"/> is <c>null</c>.
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
		/// <exception cref="System.NotSupportedException">
		/// The <see cref="ImapFolder"/> does not support mod-sequences.
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
		public override Task<IList<IMessageSummary>> FetchAsync (int min, int max, ulong modseq, MessageSummaryItems items, IEnumerable<HeaderId> headers, CancellationToken cancellationToken = default (CancellationToken))
		{
			return FetchAsync (min, max, modseq, items, ImapUtils.GetUniqueHeaders (headers), cancellationToken);
		}

		/// <summary>
		/// Fetches the message summaries for the messages between the two indexes (inclusive)
		/// that have a higher mod-sequence value than the one specified.
		/// </summary>
		/// <remarks>
		/// <para>Fetches the message summaries for the messages between the two
		/// indexes (inclusive) that have a higher mod-sequence value than the one
		/// specified.</para>
		/// <para>It should be noted that if another client has modified any message
		/// in the folder, the IMAP server may choose to return information that was
		/// not explicitly requested. It is therefore important to be prepared to
		/// handle both additional fields on a <see cref="IMessageSummary"/> for
		/// messages that were requested as well as summaries for messages that were
		/// not requested at all.</para>
		/// </remarks>
		/// <returns>An enumeration of summaries for the requested messages.</returns>
		/// <param name="min">The minimum index.</param>
		/// <param name="max">The maximum index, or <c>-1</c> to specify no upper bound.</param>
		/// <param name="modseq">The mod-sequence value.</param>
		/// <param name="items">The message summary items to fetch.</param>
		/// <param name="headers">The desired header fields.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.ArgumentOutOfRangeException">
		/// <para><paramref name="min"/> is out of range.</para>
		/// <para>-or-</para>
		/// <para><paramref name="max"/> is out of range.</para>
		/// </exception>
		/// <exception cref="System.ArgumentNullException">
		/// <paramref name="headers"/> is <c>null</c>.
		/// </exception>
		/// <exception cref="System.ArgumentException">
		/// One or more of the specified <paramref name="headers"/> is invalid.
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
		/// <exception cref="System.NotSupportedException">
		/// The <see cref="ImapFolder"/> does not support mod-sequences.
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
		public override IList<IMessageSummary> Fetch (int min, int max, ulong modseq, MessageSummaryItems items, IEnumerable<string> headers, CancellationToken cancellationToken = default (CancellationToken))
		{
			return FetchAsync (min, max, modseq, items, headers, false, cancellationToken).GetAwaiter ().GetResult ();
		}

		/// <summary>
		/// Asynchronously fetches the message summaries for the messages between the two indexes (inclusive)
		/// that have a higher mod-sequence value than the one specified.
		/// </summary>
		/// <remarks>
		/// <para>Fetches the message summaries for the messages between the two
		/// indexes (inclusive) that have a higher mod-sequence value than the one
		/// specified.</para>
		/// <para>It should be noted that if another client has modified any message
		/// in the folder, the IMAP server may choose to return information that was
		/// not explicitly requested. It is therefore important to be prepared to
		/// handle both additional fields on a <see cref="IMessageSummary"/> for
		/// messages that were requested as well as summaries for messages that were
		/// not requested at all.</para>
		/// </remarks>
		/// <returns>An enumeration of summaries for the requested messages.</returns>
		/// <param name="min">The minimum index.</param>
		/// <param name="max">The maximum index, or <c>-1</c> to specify no upper bound.</param>
		/// <param name="modseq">The mod-sequence value.</param>
		/// <param name="items">The message summary items to fetch.</param>
		/// <param name="headers">The desired header fields.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.ArgumentOutOfRangeException">
		/// <para><paramref name="min"/> is out of range.</para>
		/// <para>-or-</para>
		/// <para><paramref name="max"/> is out of range.</para>
		/// </exception>
		/// <exception cref="System.ArgumentNullException">
		/// <paramref name="headers"/> is <c>null</c>.
		/// </exception>
		/// <exception cref="System.ArgumentException">
		/// One or more of the specified <paramref name="headers"/> is invalid.
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
		/// <exception cref="System.NotSupportedException">
		/// The <see cref="ImapFolder"/> does not support mod-sequences.
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
		public override Task<IList<IMessageSummary>> FetchAsync (int min, int max, ulong modseq, MessageSummaryItems items, IEnumerable<string> headers, CancellationToken cancellationToken = default (CancellationToken))
		{
			return FetchAsync (min, max, modseq, items, headers, true, cancellationToken);
		}

		/// <summary>
		/// Create a backing stream for use with the GetMessage, GetBodyPart, and GetStream methods.
		/// </summary>
		/// <remarks>
		/// <para>Allows subclass implementations to override the type of stream
		/// created for use with the GetMessage, GetBodyPart and GetStream methods.</para>
		/// <para>This could be useful for subclass implementations that intend to implement
		/// support for caching and/or for subclass implementations that want to use
		/// temporary file streams instead of memory-based streams for larger amounts of
		/// message data.</para>
		/// <para>Subclasses that implement caching using this API should wait for
		/// <see cref="CommitStream"/> before adding the stream to their cache.</para>
		/// <para>Streams returned by this method SHOULD clean up any allocated resources
		/// such as deleting temporary files from the file system.</para>
		/// <note type="note">The <paramref name="uid"/> will not be available for the various
		/// GetMessage(), GetBodyPart() and GetStream() methods that take a message index rather
		/// than a <see cref="UniqueId"/>. It may also not be available if the IMAP server
		/// response does not specify the <c>UID</c> value prior to sending the <c>literal-string</c>
		/// token containing the message stream.</note>
		/// </remarks>
		/// <seealso cref="CommitStream"/>
		/// <returns>The stream.</returns>
		/// <param name="uid">The unique identifier of the message, if available.</param>
		/// <param name="section">The section of the message that is being fetched.</param>
		/// <param name="offset">The starting offset of the message section being fetched.</param>
		/// <param name="length">The length of the stream being fetched, measured in bytes.</param>
		protected virtual Stream CreateStream (UniqueId? uid, string section, int offset, int length)
		{
			if (length > 4096)
				return new MemoryBlockStream ();

			return new MemoryStream (length);
		}

		/// <summary>
		/// Commit a stream returned by <see cref="CreateStream"/>.
		/// </summary>
		/// <remarks>
		/// <para>Commits a stream returned by <see cref="CreateStream"/>.</para>
		/// <para>This method is called only after both the message data has successfully
		/// been written to the stream returned by <see cref="CreateStream"/> and a
		/// <see cref="UniqueId"/> has been obtained for the associated message.</para>
		/// <para>For subclasses implementing caching, this method should be used for
		/// committing the stream to their cache.</para>
		/// <note type="note">Subclass implementations may take advantage of the fact that
		/// <see cref="CommitStream"/> allows returning a new <see cref="System.IO.Stream"/>
		/// reference if they move a file on the file system and wish to return a new
		/// <see cref="System.IO.FileStream"/> based on the new path, for example.</note>
		/// </remarks>
		/// <seealso cref="CreateStream"/>
		/// <returns>The stream.</returns>
		/// <param name="stream">The stream.</param>
		/// <param name="uid">The unique identifier of the message.</param>
		/// <param name="section">The section of the message that the stream represents.</param>
		/// <param name="offset">The starting offset of the message section.</param>
		/// <param name="length">The length of the stream, measured in bytes.</param>
		protected virtual Stream CommitStream (Stream stream, UniqueId uid, string section, int offset, int length)
		{
			return stream;
		}

		async Task<HeaderList> ParseHeadersAsync (Stream stream, bool doAsync, CancellationToken cancellationToken)
		{
			try {
				return await Engine.ParseHeadersAsync (stream, doAsync, cancellationToken).ConfigureAwait (false);
			} finally {
				stream.Dispose ();
			}
		}

		async Task<MimeMessage> ParseMessageAsync (Stream stream, bool doAsync, CancellationToken cancellationToken)
		{
			bool dispose = !(stream is MemoryStream || stream is MemoryBlockStream);

			try {
				return await Engine.ParseMessageAsync (stream, !dispose, doAsync, cancellationToken).ConfigureAwait (false);
			} finally {
				if (dispose)
					stream.Dispose ();
			}
		}

		async Task<MimeEntity> ParseEntityAsync (Stream stream, bool dispose, bool doAsync, CancellationToken cancellationToken)
		{
			try {
				return await Engine.ParseEntityAsync (stream, !dispose, doAsync, cancellationToken).ConfigureAwait (false);
			} finally {
				if (dispose)
					stream.Dispose ();
			}
		}

		class Section
		{
			public int Index;
			public UniqueId? UniqueId;
			public Stream Stream;
			public string Name;
			public int Offset;
			public int Length;

			public Section (Stream stream, int index, UniqueId? uid, string name, int offset, int length)
			{
				Stream = stream;
				Offset = offset;
				Length = length;
				UniqueId = uid;
				Index = index;
				Name = name;
			}
		}

		abstract class FetchStreamContextBase : IDisposable
		{
			protected static readonly Task Complete = Task.FromResult (true);
			public readonly List<Section> Sections = new List<Section> ();
			readonly ITransferProgress progress;

			public FetchStreamContextBase (ITransferProgress progress)
			{
				this.progress = progress;
			}

			public abstract Task AddAsync (Section section, bool doAsync, CancellationToken cancellationToken);

			public virtual bool Contains (int index, string specifier, out Section section)
			{
				section = null;
				return false;
			}

			public abstract Task SetUniqueIdAsync (int index, UniqueId uid, bool doAsync, CancellationToken cancellationToken);

			public void Report (long nread, long total)
			{
				if (progress == null)
					return;

				progress.Report (nread, total);
			}

			public void Dispose ()
			{
				for (int i = 0; i < Sections.Count; i++) {
					var section = Sections[i];

					try {
						section.Stream.Dispose ();
					} catch (IOException) {
					}
				}
			}
		}

		class FetchStreamContext : FetchStreamContextBase
		{
			public FetchStreamContext (ITransferProgress progress) : base (progress)
			{
			}

			public override Task AddAsync (Section section, bool doAsync, CancellationToken cancellationToken)
			{
				Sections.Add (section);
				return Complete;
			}

			public bool TryGetSection (UniqueId uid, string specifier, out Section section, bool remove = false)
			{
				for (int i = 0; i < Sections.Count; i++) {
					var item = Sections[i];

					if (!item.UniqueId.HasValue || item.UniqueId.Value != uid)
						continue;

					if (item.Name.Equals (specifier, StringComparison.OrdinalIgnoreCase)) {
						if (remove)
							Sections.RemoveAt (i);

						section = item;
						return true;
					}
				}

				section = null;

				return false;
			}

			public bool TryGetSection (int index, string specifier, out Section section, bool remove = false)
			{
				for (int i = 0; i < Sections.Count; i++) {
					var item = Sections[i];

					if (item.Index != index)
						continue;

					if (item.Name.Equals (specifier, StringComparison.OrdinalIgnoreCase)) {
						if (remove)
							Sections.RemoveAt (i);

						section = item;
						return true;
					}
				}

				section = null;

				return false;
			}

			public override Task SetUniqueIdAsync (int index, UniqueId uid, bool doAsync, CancellationToken cancellationToken)
			{
				for (int i = 0; i < Sections.Count; i++) {
					if (Sections[i].Index == index)
						Sections[i].UniqueId = uid;
				}

				return Complete;
			}
		}

		async Task FetchStreamAsync (ImapEngine engine, ImapCommand ic, int index, bool doAsync)
		{
			var token = await engine.ReadTokenAsync (doAsync, ic.CancellationToken).ConfigureAwait (false);
			var annotations = new AnnotationsChangedEventArgs (index);
			var labels = new MessageLabelsChangedEventArgs (index);
			var flags = new MessageFlagsChangedEventArgs (index);
			var modSeq = new ModSeqChangedEventArgs (index);
			var ctx = (FetchStreamContextBase) ic.UserData;
			var sectionBuilder = new StringBuilder ();
			bool annotationsChanged = false;
			bool modSeqChanged = false;
			bool labelsChanged = false;
			bool flagsChanged = false;
			long nread = 0, size = 0;
			UniqueId? uid = null;
			Section section;
			Stream stream;
			string name;
			byte[] buf;
			int n;

			ImapEngine.AssertToken (token, ImapTokenType.OpenParen, ImapEngine.GenericUntaggedResponseSyntaxErrorFormat, "FETCH", token);

			do {
				token = await engine.ReadTokenAsync (doAsync, ic.CancellationToken).ConfigureAwait (false);

				if (token.Type == ImapTokenType.Eoln) {
					// Note: Most likely the the message body was calculated to be 1 or 2 bytes too
					// short (e.g. did not include the trailing <CRLF>) and that is the EOLN we just
					// reached. Ignore it and continue as normal.
					//
					// See https://github.com/jstedfast/MailKit/issues/954 for details.
					token = await engine.ReadTokenAsync (doAsync, ic.CancellationToken).ConfigureAwait (false);
				}

				if (token.Type == ImapTokenType.CloseParen)
					break;

				ImapEngine.AssertToken (token, ImapTokenType.Atom, ImapEngine.GenericUntaggedResponseSyntaxErrorFormat, "FETCH", token);

				var atom = (string) token.Value;
				int offset = 0, length;
				ulong modseq;
				uint value;

				switch (atom.ToUpperInvariant ()) {
				case "BODY":
					token = await engine.ReadTokenAsync (doAsync, ic.CancellationToken).ConfigureAwait (false);

					ImapEngine.AssertToken (token, ImapTokenType.OpenBracket, ImapEngine.GenericItemSyntaxErrorFormat, atom, token);

					sectionBuilder.Clear ();

					do {
						token = await engine.ReadTokenAsync (doAsync, ic.CancellationToken).ConfigureAwait (false);

						if (token.Type == ImapTokenType.CloseBracket)
							break;

						if (token.Type == ImapTokenType.OpenParen) {
							sectionBuilder.Append (" (");

							do {
								token = await engine.ReadTokenAsync (doAsync, ic.CancellationToken).ConfigureAwait (false);

								if (token.Type == ImapTokenType.CloseParen)
									break;

								// the header field names will generally be atoms or qstrings but may also be literals
								switch (token.Type) {
								case ImapTokenType.Literal:
									sectionBuilder.Append (await engine.ReadLiteralAsync (doAsync, ic.CancellationToken).ConfigureAwait (false));
									break;
								case ImapTokenType.QString:
								case ImapTokenType.Atom:
									sectionBuilder.Append ((string) token.Value);
									break;
								default:
									throw ImapEngine.UnexpectedToken (ImapEngine.GenericItemSyntaxErrorFormat, atom, token);
								}

								sectionBuilder.Append (' ');
							} while (true);

							if (sectionBuilder[sectionBuilder.Length - 1] == ' ')
								sectionBuilder.Length--;

							sectionBuilder.Append (')');
						} else {
							ImapEngine.AssertToken (token, ImapTokenType.Atom, ImapEngine.GenericItemSyntaxErrorFormat, atom, token);

							sectionBuilder.Append ((string) token.Value);
						}
					} while (true);

					ImapEngine.AssertToken (token, ImapTokenType.CloseBracket, ImapEngine.GenericItemSyntaxErrorFormat, atom, token);

					token = await engine.ReadTokenAsync (doAsync, ic.CancellationToken).ConfigureAwait (false);

					if (token.Type == ImapTokenType.Atom) {
						// this might be a region ("<###>")
						var expr = (string) token.Value;

						if (expr.Length > 2 && expr[0] == '<' && expr[expr.Length - 1] == '>') {
							var region = expr.Substring (1, expr.Length - 2);

							int.TryParse (region, NumberStyles.None, CultureInfo.InvariantCulture, out offset);

							token = await engine.ReadTokenAsync (doAsync, ic.CancellationToken).ConfigureAwait (false);
						}
					}

					name = sectionBuilder.ToString ();

					switch (token.Type) {
					case ImapTokenType.Literal:
						length = (int) token.Value;
						size += length;

						stream = CreateStream (uid, name, offset, length);

						buf = ArrayPool<byte>.Shared.Rent (BufferSize);

						try {
							do {
								if (doAsync)
									n = await engine.Stream.ReadAsync (buf, 0, BufferSize, ic.CancellationToken).ConfigureAwait (false);
								else
									n = engine.Stream.Read (buf, 0, BufferSize, ic.CancellationToken);

								if (n > 0) {
									stream.Write (buf, 0, n);
									nread += n;

									ctx.Report (nread, size);
								} else {
									break;
								}
							} while (true);

							stream.Position = 0;
						} catch {
							stream.Dispose ();
							throw;
						} finally {
							ArrayPool<byte>.Shared.Return (buf);
						}
						break;
					case ImapTokenType.QString:
					case ImapTokenType.Atom:
						buf = Encoding.UTF8.GetBytes ((string) token.Value);
						length = buf.Length;
						nread += length;
						size += length;

						stream = CreateStream (uid, name, offset, length);

						try {
							stream.Write (buf, 0, length);
							ctx.Report (nread, size);
							stream.Position = 0;
						} catch {
							stream.Dispose ();
							throw;
						}
						break;
					case ImapTokenType.Nil:
						stream = CreateStream (uid, name, offset, 0);
						length = 0;
						break;
					default:
						throw ImapEngine.UnexpectedToken (ImapEngine.GenericItemSyntaxErrorFormat, atom, token);
					}

					if (uid.HasValue)
						stream = CommitStream (stream, uid.Value, name, offset, length);

					// prevent leaks in the (invalid) case where a section may be returned twice
					if (ctx.Contains (index, name, out section))
						section.Stream.Dispose ();

					section = new Section (stream, index, uid, name, offset, length);
					await ctx.AddAsync (section, doAsync, ic.CancellationToken).ConfigureAwait (false);
					break;
				case "UID":
					token = await engine.ReadTokenAsync (doAsync, ic.CancellationToken).ConfigureAwait (false);

					value = ImapEngine.ParseNumber (token, true, ImapEngine.GenericItemSyntaxErrorFormat, atom, token);
					uid = new UniqueId (UidValidity, value);

					await ctx.SetUniqueIdAsync (index, uid.Value, doAsync, ic.CancellationToken).ConfigureAwait (false);

					annotations.UniqueId = uid.Value;
					modSeq.UniqueId = uid.Value;
					labels.UniqueId = uid.Value;
					flags.UniqueId = uid.Value;
					break;
				case "MODSEQ":
					token = await engine.ReadTokenAsync (doAsync, ic.CancellationToken).ConfigureAwait (false);

					ImapEngine.AssertToken (token, ImapTokenType.OpenParen, ImapEngine.GenericItemSyntaxErrorFormat, atom, token);

					token = await engine.ReadTokenAsync (doAsync, ic.CancellationToken).ConfigureAwait (false);

					modseq = ImapEngine.ParseNumber64 (token, false, ImapEngine.GenericItemSyntaxErrorFormat, atom, token);

					token = await engine.ReadTokenAsync (doAsync, ic.CancellationToken).ConfigureAwait (false);

					ImapEngine.AssertToken (token, ImapTokenType.CloseParen, ImapEngine.GenericItemSyntaxErrorFormat, atom, token);

					if (modseq > HighestModSeq)
						UpdateHighestModSeq (modseq);

					annotations.ModSeq = modseq;
					modSeq.ModSeq = modseq;
					labels.ModSeq = modseq;
					flags.ModSeq = modseq;
					modSeqChanged = true;
					break;
				case "FLAGS":
					// even though we didn't request this piece of information, the IMAP server
					// may send it if another client has recently modified the message flags.
					flags.Flags = await ImapUtils.ParseFlagsListAsync (engine, atom, flags.Keywords, doAsync, ic.CancellationToken).ConfigureAwait (false);
					flagsChanged = true;
					break;
				case "X-GM-LABELS":
					// even though we didn't request this piece of information, the IMAP server
					// may send it if another client has recently modified the message labels.
					labels.Labels = await ImapUtils.ParseLabelsListAsync (engine, doAsync, ic.CancellationToken).ConfigureAwait (false);
					labelsChanged = true;
					break;
				case "ANNOTATION":
					// even though we didn't request this piece of information, the IMAP server
					// may send it if another client has recently modified the message annotations.
					annotations.Annotations = await ImapUtils.ParseAnnotationsAsync (engine, doAsync, ic.CancellationToken).ConfigureAwait (false);
					annotationsChanged = true;
					break;
				default:
					// Unexpected or unknown token (such as XAOL.SPAM.REASON or XAOL-MSGID). Simply read 1 more token (the argument) and ignore.
					token = await engine.ReadTokenAsync (doAsync, ic.CancellationToken).ConfigureAwait (false);

					if (token.Type == ImapTokenType.OpenParen)
						await SkipParenthesizedList (engine, doAsync, ic.CancellationToken).ConfigureAwait (false);
					break;
				}
			} while (true);

			ImapEngine.AssertToken (token, ImapTokenType.CloseParen, ImapEngine.GenericUntaggedResponseSyntaxErrorFormat, "FETCH", token);

			if (flagsChanged)
				OnMessageFlagsChanged (flags);

			if (labelsChanged)
				OnMessageLabelsChanged (labels);

			if (annotationsChanged)
				OnAnnotationsChanged (annotations);

			if (modSeqChanged)
				OnModSeqChanged (modSeq);
		}

		static string GetBodyPartQuery (string partSpec, bool headersOnly, out string[] tags)
		{
			string query;

			if (headersOnly) {
				tags = new string[1];

				if (partSpec.Length > 0) {
					query = string.Format ("BODY.PEEK[{0}.MIME]", partSpec);
					tags[0] = partSpec + ".MIME";
				} else {
					query = "BODY.PEEK[HEADER]";
					tags[0] = "HEADER";
				}
			} else {
				tags = new string[2];

				if (partSpec.Length > 0) {
					tags[0] = partSpec + ".MIME";
					tags[1] = partSpec;
				} else {
					tags[0] = "HEADER";
					tags[1] = "TEXT";
				}

				query = string.Format ("BODY.PEEK[{0}] BODY.PEEK[{1}]", tags[0], tags[1]);
			}

			return query;
		}

		async Task<HeaderList> GetHeadersAsync (UniqueId uid, bool doAsync, CancellationToken cancellationToken, ITransferProgress progress)
		{
			if (!uid.IsValid)
				throw new ArgumentException ("The uid is invalid.", nameof (uid));

			CheckState (true, false);

			var ic = new ImapCommand (Engine, cancellationToken, this, "UID FETCH %u (BODY.PEEK[HEADER])\r\n", uid.Id);
			var ctx = new FetchStreamContext (progress);
			Section section;

			ic.RegisterUntaggedHandler ("FETCH", FetchStreamAsync);
			ic.UserData = ctx;

			Engine.QueueCommand (ic);

			try {
				await Engine.RunAsync (ic, doAsync).ConfigureAwait (false);

				ProcessResponseCodes (ic, null);

				if (ic.Response != ImapCommandResponse.Ok)
					throw ImapCommandException.Create ("FETCH", ic);

				if (!ctx.TryGetSection (uid, "HEADER", out section, true))
					throw new MessageNotFoundException ("The IMAP server did not return the requested message headers.");
			} finally {
				ctx.Dispose ();
			}

			return await ParseHeadersAsync (section.Stream, doAsync, cancellationToken).ConfigureAwait (false);
		}

		async Task<HeaderList> GetHeadersAsync (UniqueId uid, string partSpecifier, bool doAsync, CancellationToken cancellationToken, ITransferProgress progress)
		{
			if (!uid.IsValid)
				throw new ArgumentException ("The uid is invalid.", nameof (uid));

			if (partSpecifier == null)
				throw new ArgumentNullException (nameof (partSpecifier));

			CheckState (true, false);

			string[] tags;

			var command = string.Format ("UID FETCH {0} ({1})\r\n", uid, GetBodyPartQuery (partSpecifier, true, out tags));
			var ic = new ImapCommand (Engine, cancellationToken, this, command);
			var ctx = new FetchStreamContext (progress);
			Section section;

			ic.RegisterUntaggedHandler ("FETCH", FetchStreamAsync);
			ic.UserData = ctx;

			Engine.QueueCommand (ic);

			try {
				await Engine.RunAsync (ic, doAsync).ConfigureAwait (false);

				ProcessResponseCodes (ic, null);

				if (ic.Response != ImapCommandResponse.Ok)
					throw ImapCommandException.Create ("FETCH", ic);

				if (!ctx.TryGetSection (uid, tags[0], out section, true))
					throw new MessageNotFoundException ("The IMAP server did not return the requested body part headers.");
			} finally {
				ctx.Dispose ();
			}

			return await ParseHeadersAsync (section.Stream, doAsync, cancellationToken).ConfigureAwait (false);
		}

		/// <summary>
		/// Get the specified message headers.
		/// </summary>
		/// <remarks>
		/// Gets the specified message headers.
		/// </remarks>
		/// <returns>The message headers.</returns>
		/// <param name="uid">The UID of the message.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <param name="progress">The progress reporting mechanism.</param>
		/// <exception cref="System.ArgumentException">
		/// <paramref name="uid"/> is invalid.
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
		/// <exception cref="MessageNotFoundException">
		/// The IMAP server did not return the requested message headers.
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
		public override HeaderList GetHeaders (UniqueId uid, CancellationToken cancellationToken = default (CancellationToken), ITransferProgress progress = null)
		{
			return GetHeadersAsync (uid, false, cancellationToken, progress).GetAwaiter ().GetResult ();
		}

		/// <summary>
		/// Asynchronously get the specified message headers.
		/// </summary>
		/// <remarks>
		/// Gets the specified message headers.
		/// </remarks>
		/// <returns>The message headers.</returns>
		/// <param name="uid">The UID of the message.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <param name="progress">The progress reporting mechanism.</param>
		/// <exception cref="System.ArgumentException">
		/// <paramref name="uid"/> is invalid.
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
		/// <exception cref="MessageNotFoundException">
		/// The IMAP server did not return the requested message headers.
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
		public override Task<HeaderList> GetHeadersAsync (UniqueId uid, CancellationToken cancellationToken = default (CancellationToken), ITransferProgress progress = null)
		{
			return GetHeadersAsync (uid, true, cancellationToken, progress);
		}

		/// <summary>
		/// Get the specified body part headers.
		/// </summary>
		/// <remarks>
		/// Gets the specified body part headers.
		/// </remarks>
		/// <returns>The body part headers.</returns>
		/// <param name="uid">The UID of the message.</param>
		/// <param name="partSpecifier">The body part specifier.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <param name="progress">The progress reporting mechanism.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <paramref name="partSpecifier"/> is <c>null</c>.
		/// </exception>
		/// <exception cref="System.ArgumentException">
		/// <paramref name="uid"/> is invalid.
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
		/// <exception cref="MessageNotFoundException">
		/// The IMAP server did not return the requested body part headers.
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
		public virtual HeaderList GetHeaders (UniqueId uid, string partSpecifier, CancellationToken cancellationToken = default (CancellationToken), ITransferProgress progress = null)
		{
			return GetHeadersAsync (uid, partSpecifier, false, cancellationToken, progress).GetAwaiter ().GetResult ();
		}

		/// <summary>
		/// Asynchronously get the specified body part headers.
		/// </summary>
		/// <remarks>
		/// Gets the specified body part headers.
		/// </remarks>
		/// <returns>The body part headers.</returns>
		/// <param name="uid">The UID of the message.</param>
		/// <param name="partSpecifier">The body part specifier.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <param name="progress">The progress reporting mechanism.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <paramref name="partSpecifier"/> is <c>null</c>.
		/// </exception>
		/// <exception cref="System.ArgumentException">
		/// <paramref name="uid"/> is invalid.
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
		/// <exception cref="MessageNotFoundException">
		/// The IMAP server did not return the requested body part headers.
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
		public virtual Task<HeaderList> GetHeadersAsync (UniqueId uid, string partSpecifier, CancellationToken cancellationToken = default (CancellationToken), ITransferProgress progress = null)
		{
			return GetHeadersAsync (uid, partSpecifier, true, cancellationToken, progress);
		}

		/// <summary>
		/// Get the specified body part headers.
		/// </summary>
		/// <remarks>
		/// Gets the specified body part headers.
		/// </remarks>
		/// <returns>The body part headers.</returns>
		/// <param name="uid">The UID of the message.</param>
		/// <param name="part">The body part.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <param name="progress">The progress reporting mechanism.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <paramref name="part"/> is <c>null</c>.
		/// </exception>
		/// <exception cref="System.ArgumentException">
		/// <paramref name="uid"/> is invalid.
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
		/// <exception cref="MessageNotFoundException">
		/// The IMAP server did not return the requested body part headers.
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
		public override HeaderList GetHeaders (UniqueId uid, BodyPart part, CancellationToken cancellationToken = default (CancellationToken), ITransferProgress progress = null)
		{
			if (!uid.IsValid)
				throw new ArgumentException ("The uid is invalid.", nameof (uid));

			if (part == null)
				throw new ArgumentNullException (nameof (part));

			return GetHeaders (uid, part.PartSpecifier, cancellationToken, progress);
		}

		/// <summary>
		/// Asynchronously get the specified body part headers.
		/// </summary>
		/// <remarks>
		/// Gets the specified body part headers.
		/// </remarks>
		/// <returns>The body part headers.</returns>
		/// <param name="uid">The UID of the message.</param>
		/// <param name="part">The body part.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <param name="progress">The progress reporting mechanism.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <paramref name="part"/> is <c>null</c>.
		/// </exception>
		/// <exception cref="System.ArgumentException">
		/// <paramref name="uid"/> is invalid.
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
		/// <exception cref="MessageNotFoundException">
		/// The IMAP server did not return the requested body part headers.
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
		public override Task<HeaderList> GetHeadersAsync (UniqueId uid, BodyPart part, CancellationToken cancellationToken = default (CancellationToken), ITransferProgress progress = null)
		{
			if (!uid.IsValid)
				throw new ArgumentException ("The uid is invalid.", nameof (uid));

			if (part == null)
				throw new ArgumentNullException (nameof (part));

			return GetHeadersAsync (uid, part.PartSpecifier, cancellationToken, progress);
		}

		async Task<HeaderList> GetHeadersAsync (int index, bool doAsync, CancellationToken cancellationToken, ITransferProgress progress)
		{
			if (index < 0 || index >= Count)
				throw new ArgumentOutOfRangeException (nameof (index));

			CheckState (true, false);

			var ic = new ImapCommand (Engine, cancellationToken, this, "FETCH %d (BODY.PEEK[HEADER])\r\n", index + 1);
			var ctx = new FetchStreamContext (progress);
			Section section;

			ic.RegisterUntaggedHandler ("FETCH", FetchStreamAsync);
			ic.UserData = ctx;

			Engine.QueueCommand (ic);

			try {
				await Engine.RunAsync (ic, doAsync).ConfigureAwait (false);

				ProcessResponseCodes (ic, null);

				if (ic.Response != ImapCommandResponse.Ok)
					throw ImapCommandException.Create ("FETCH", ic);

				if (!ctx.TryGetSection (index, "HEADER", out section, true))
					throw new MessageNotFoundException ("The IMAP server did not return the requested message.");
			} finally {
				ctx.Dispose ();
			}

			return await ParseHeadersAsync (section.Stream, doAsync, cancellationToken).ConfigureAwait (false);
		}

		async Task<HeaderList> GetHeadersAsync (int index, string partSpecifier, bool doAsync, CancellationToken cancellationToken, ITransferProgress progress)
		{
			if (index < 0 || index >= Count)
				throw new ArgumentOutOfRangeException (nameof (index));

			if (partSpecifier == null)
				throw new ArgumentNullException (nameof (partSpecifier));

			CheckState (true, false);

			string[] tags;

			var seqid = (index + 1).ToString (CultureInfo.InvariantCulture);
			var command = string.Format ("FETCH {0} ({1})\r\n", seqid, GetBodyPartQuery (partSpecifier, true, out tags));
			var ic = new ImapCommand (Engine, cancellationToken, this, command);
			var ctx = new FetchStreamContext (progress);
			Section section;

			ic.RegisterUntaggedHandler ("FETCH", FetchStreamAsync);
			ic.UserData = ctx;

			Engine.QueueCommand (ic);

			try {
				await Engine.RunAsync (ic, doAsync).ConfigureAwait (false);

				ProcessResponseCodes (ic, null);

				if (ic.Response != ImapCommandResponse.Ok)
					throw ImapCommandException.Create ("FETCH", ic);

				if (!ctx.TryGetSection (index, tags[0], out section, true))
					throw new MessageNotFoundException ("The IMAP server did not return the requested body part headers.");
			} finally {
				ctx.Dispose ();
			}

			return await ParseHeadersAsync (section.Stream, doAsync, cancellationToken).ConfigureAwait (false);
		}

		/// <summary>
		/// Get the specified message headers.
		/// </summary>
		/// <remarks>
		/// Gets the specified message headers.
		/// </remarks>
		/// <returns>The message headers.</returns>
		/// <param name="index">The index of the message.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <param name="progress">The progress reporting mechanism.</param>
		/// <exception cref="System.ArgumentOutOfRangeException">
		/// <paramref name="index"/> is out of range.
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
		/// <exception cref="MessageNotFoundException">
		/// The IMAP server did not return the requested message headers.
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
		public override HeaderList GetHeaders (int index, CancellationToken cancellationToken = default (CancellationToken), ITransferProgress progress = null)
		{
			return GetHeadersAsync (index, false, cancellationToken, progress).GetAwaiter ().GetResult ();
		}

		/// <summary>
		/// Asynchronously get the specified message headers.
		/// </summary>
		/// <remarks>
		/// Gets the specified message headers.
		/// </remarks>
		/// <returns>The message headers.</returns>
		/// <param name="index">The index of the message.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <param name="progress">The progress reporting mechanism.</param>
		/// <exception cref="System.ArgumentOutOfRangeException">
		/// <paramref name="index"/> is out of range.
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
		/// <exception cref="MessageNotFoundException">
		/// The IMAP server did not return the requested message headers.
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
		public override Task<HeaderList> GetHeadersAsync (int index, CancellationToken cancellationToken = default (CancellationToken), ITransferProgress progress = null)
		{
			return GetHeadersAsync (index, true, cancellationToken, progress);
		}

		/// <summary>
		/// Get the specified body part headers.
		/// </summary>
		/// <remarks>
		/// Gets the specified body part headers.
		/// </remarks>
		/// <returns>The body part headers.</returns>
		/// <param name="index">The index of the message.</param>
		/// <param name="partSpecifier">The body part specifier.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <param name="progress">The progress reporting mechanism.</param>
		/// <exception cref="System.ArgumentOutOfRangeException">
		/// <paramref name="index"/> is out of range.
		/// </exception>
		/// <exception cref="System.ArgumentNullException">
		/// <paramref name="partSpecifier"/> is <c>null</c>.
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
		/// <exception cref="MessageNotFoundException">
		/// The IMAP server did not return the requested body part headers.
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
		public virtual HeaderList GetHeaders (int index, string partSpecifier, CancellationToken cancellationToken = default (CancellationToken), ITransferProgress progress = null)
		{
			return GetHeadersAsync (index, partSpecifier, false, cancellationToken, progress).GetAwaiter ().GetResult ();
		}

		/// <summary>
		/// Asynchronously get the specified body part headers.
		/// </summary>
		/// <remarks>
		/// Gets the specified body part headers.
		/// </remarks>
		/// <returns>The body part headers.</returns>
		/// <param name="index">The index of the message.</param>
		/// <param name="partSpecifier">The body part specifier.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <param name="progress">The progress reporting mechanism.</param>
		/// <exception cref="System.ArgumentOutOfRangeException">
		/// <paramref name="index"/> is out of range.
		/// </exception>
		/// <exception cref="System.ArgumentNullException">
		/// <paramref name="partSpecifier"/> is <c>null</c>.
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
		/// <exception cref="MessageNotFoundException">
		/// The IMAP server did not return the requested body part headers.
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
		public virtual Task<HeaderList> GetHeadersAsync (int index, string partSpecifier, CancellationToken cancellationToken = default (CancellationToken), ITransferProgress progress = null)
		{
			return GetHeadersAsync (index, partSpecifier, true, cancellationToken, progress);
		}

		/// <summary>
		/// Get the specified body part headers.
		/// </summary>
		/// <remarks>
		/// Gets the specified body part headers.
		/// </remarks>
		/// <returns>The body part headers.</returns>
		/// <param name="index">The index of the message.</param>
		/// <param name="part">The body part.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <param name="progress">The progress reporting mechanism.</param>
		/// <exception cref="System.ArgumentOutOfRangeException">
		/// <paramref name="index"/> is out of range.
		/// </exception>
		/// <exception cref="System.ArgumentNullException">
		/// <paramref name="part"/> is <c>null</c>.
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
		/// <exception cref="MessageNotFoundException">
		/// The IMAP server did not return the requested body part headers.
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
		public override HeaderList GetHeaders (int index, BodyPart part, CancellationToken cancellationToken = default (CancellationToken), ITransferProgress progress = null)
		{
			if (index < 0 || index >= Count)
				throw new ArgumentOutOfRangeException (nameof (index));

			if (part == null)
				throw new ArgumentNullException (nameof (part));

			return GetHeaders (index, part.PartSpecifier, cancellationToken, progress);
		}

		/// <summary>
		/// Asynchronously get the specified body part headers.
		/// </summary>
		/// <remarks>
		/// Gets the specified body part headers.
		/// </remarks>
		/// <returns>The body part headers.</returns>
		/// <param name="index">The index of the message.</param>
		/// <param name="part">The body part.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <param name="progress">The progress reporting mechanism.</param>
		/// <exception cref="System.ArgumentOutOfRangeException">
		/// <paramref name="index"/> is out of range.
		/// </exception>
		/// <exception cref="System.ArgumentNullException">
		/// <paramref name="part"/> is <c>null</c>.
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
		/// <exception cref="MessageNotFoundException">
		/// The IMAP server did not return the requested body part headers.
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
		public override Task<HeaderList> GetHeadersAsync (int index, BodyPart part, CancellationToken cancellationToken = default (CancellationToken), ITransferProgress progress = null)
		{
			if (index < 0 || index >= Count)
				throw new ArgumentOutOfRangeException (nameof (index));

			if (part == null)
				throw new ArgumentNullException (nameof (part));

			return GetHeadersAsync (index, part.PartSpecifier, cancellationToken, progress);
		}

		async Task<MimeMessage> GetMessageAsync (UniqueId uid, bool doAsync, CancellationToken cancellationToken, ITransferProgress progress)
		{
			if (!uid.IsValid)
				throw new ArgumentException ("The uid is invalid.", nameof (uid));

			CheckState (true, false);

			var ic = new ImapCommand (Engine, cancellationToken, this, "UID FETCH %u (BODY.PEEK[])\r\n", uid.Id);
			var ctx = new FetchStreamContext (progress);
			Section section;

			ic.RegisterUntaggedHandler ("FETCH", FetchStreamAsync);
			ic.UserData = ctx;

			Engine.QueueCommand (ic);

			try {
				await Engine.RunAsync (ic, doAsync).ConfigureAwait (false);

				ProcessResponseCodes (ic, null);

				if (ic.Response != ImapCommandResponse.Ok)
					throw ImapCommandException.Create ("FETCH", ic);

				if (!ctx.TryGetSection (uid, string.Empty, out section, true))
					throw new MessageNotFoundException ("The IMAP server did not return the requested message.");
			} finally {
				ctx.Dispose ();
			}

			return await ParseMessageAsync (section.Stream, doAsync, cancellationToken).ConfigureAwait (false);
		}

		/// <summary>
		/// Get the specified message.
		/// </summary>
		/// <remarks>
		/// Gets the specified message.
		/// </remarks>
		/// <returns>The message.</returns>
		/// <param name="uid">The UID of the message.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <param name="progress">The progress reporting mechanism.</param>
		/// <exception cref="System.ArgumentException">
		/// <paramref name="uid"/> is invalid.
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
		/// <exception cref="MessageNotFoundException">
		/// The IMAP server did not return the requested message.
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
		public override MimeMessage GetMessage (UniqueId uid, CancellationToken cancellationToken = default (CancellationToken), ITransferProgress progress = null)
		{
			return GetMessageAsync (uid, false, cancellationToken, progress).GetAwaiter ().GetResult ();
		}

		/// <summary>
		/// Asynchronously get the specified message.
		/// </summary>
		/// <remarks>
		/// Gets the specified message.
		/// </remarks>
		/// <returns>The message.</returns>
		/// <param name="uid">The UID of the message.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <param name="progress">The progress reporting mechanism.</param>
		/// <exception cref="System.ArgumentException">
		/// <paramref name="uid"/> is invalid.
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
		/// <exception cref="MessageNotFoundException">
		/// The IMAP server did not return the requested message.
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
		public override Task<MimeMessage> GetMessageAsync (UniqueId uid, CancellationToken cancellationToken = default (CancellationToken), ITransferProgress progress = null)
		{
			return GetMessageAsync (uid, true, cancellationToken, progress);
		}

		async Task<MimeMessage> GetMessageAsync (int index, bool doAsync, CancellationToken cancellationToken, ITransferProgress progress)
		{
			if (index < 0 || index >= Count)
				throw new ArgumentOutOfRangeException (nameof (index));

			CheckState (true, false);

			var ic = new ImapCommand (Engine, cancellationToken, this, "FETCH %d (BODY.PEEK[])\r\n", index + 1);
			var ctx = new FetchStreamContext (progress);
			Section section;

			ic.RegisterUntaggedHandler ("FETCH", FetchStreamAsync);
			ic.UserData = ctx;

			Engine.QueueCommand (ic);

			try {
				await Engine.RunAsync (ic, doAsync).ConfigureAwait (false);

				ProcessResponseCodes (ic, null);

				if (ic.Response != ImapCommandResponse.Ok)
					throw ImapCommandException.Create ("FETCH", ic);

				if (!ctx.TryGetSection (index, string.Empty, out section, true))
					throw new MessageNotFoundException ("The IMAP server did not return the requested message.");
			} finally {
				ctx.Dispose ();
			}

			return await ParseMessageAsync (section.Stream, doAsync, cancellationToken).ConfigureAwait (false);
		}

		/// <summary>
		/// Get the specified message.
		/// </summary>
		/// <remarks>
		/// Gets the specified message.
		/// </remarks>
		/// <returns>The message.</returns>
		/// <param name="index">The index of the message.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <param name="progress">The progress reporting mechanism.</param>
		/// <exception cref="System.ArgumentOutOfRangeException">
		/// <paramref name="index"/> is out of range.
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
		/// <exception cref="MessageNotFoundException">
		/// The IMAP server did not return the requested message.
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
		public override MimeMessage GetMessage (int index, CancellationToken cancellationToken = default (CancellationToken), ITransferProgress progress = null)
		{
			return GetMessageAsync (index, false, cancellationToken, progress).GetAwaiter ().GetResult ();
		}

		/// <summary>
		/// Asynchronously get the specified message.
		/// </summary>
		/// <remarks>
		/// Gets the specified message.
		/// </remarks>
		/// <returns>The message.</returns>
		/// <param name="index">The index of the message.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <param name="progress">The progress reporting mechanism.</param>
		/// <exception cref="System.ArgumentOutOfRangeException">
		/// <paramref name="index"/> is out of range.
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
		/// <exception cref="MessageNotFoundException">
		/// The IMAP server did not return the requested message.
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
		public override Task<MimeMessage> GetMessageAsync (int index, CancellationToken cancellationToken = default (CancellationToken), ITransferProgress progress = null)
		{
			return GetMessageAsync (index, true, cancellationToken, progress);
		}

		async Task<MimeEntity> GetBodyPartAsync (UniqueId uid, string partSpecifier, bool doAsync, CancellationToken cancellationToken, ITransferProgress progress)
		{
			if (!uid.IsValid)
				throw new ArgumentException ("The uid is invalid.", nameof (uid));

			if (partSpecifier == null)
				throw new ArgumentNullException (nameof (partSpecifier));

			CheckState (true, false);

			string[] tags;

			var command = string.Format ("UID FETCH {0} ({1})\r\n", uid, GetBodyPartQuery (partSpecifier, false, out tags));
			var ic = new ImapCommand (Engine, cancellationToken, this, command);
			var ctx = new FetchStreamContext (progress);
			ChainedStream chained = null;
			bool dispose = false;
			Section section;

			ic.RegisterUntaggedHandler ("FETCH", FetchStreamAsync);
			ic.UserData = ctx;

			Engine.QueueCommand (ic);

			try {
				await Engine.RunAsync (ic, doAsync).ConfigureAwait (false);

				ProcessResponseCodes (ic, null);

				if (ic.Response != ImapCommandResponse.Ok)
					throw ImapCommandException.Create ("FETCH", ic);

				chained = new ChainedStream ();

				foreach (var tag in tags) {
					if (!ctx.TryGetSection (uid, tag, out section, true))
						throw new MessageNotFoundException ("The IMAP server did not return the requested body part.");

					if (!(section.Stream is MemoryStream || section.Stream is MemoryBlockStream))
						dispose = true;

					chained.Add (section.Stream);
				}
			} catch {
				if (chained != null)
					chained.Dispose ();

				throw;
			} finally {
				ctx.Dispose ();
			}

			var entity = await ParseEntityAsync (chained, dispose, doAsync, cancellationToken).ConfigureAwait (false);

			if (partSpecifier.Length == 0) {
				for (int i = entity.Headers.Count; i > 0; i--) {
					var header = entity.Headers[i - 1];

					if (!header.Field.StartsWith ("Content-", StringComparison.OrdinalIgnoreCase))
						entity.Headers.RemoveAt (i - 1);
				}
			}

			return entity;
		}

		/// <summary>
		/// Get the specified body part.
		/// </summary>
		/// <remarks>
		/// Gets the specified body part.
		/// </remarks>
		/// <returns>The body part.</returns>
		/// <param name="uid">The UID of the message.</param>
		/// <param name="partSpecifier">The body part specifier.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <param name="progress">The progress reporting mechanism.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <paramref name="partSpecifier"/> is <c>null</c>.
		/// </exception>
		/// <exception cref="System.ArgumentException">
		/// <paramref name="uid"/> is invalid.
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
		/// <exception cref="MessageNotFoundException">
		/// The IMAP server did not return the requested message body.
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
		public virtual MimeEntity GetBodyPart (UniqueId uid, string partSpecifier, CancellationToken cancellationToken = default (CancellationToken), ITransferProgress progress = null)
		{
			return GetBodyPartAsync (uid, partSpecifier, false, cancellationToken, progress).GetAwaiter ().GetResult ();
		}

		/// <summary>
		/// Asynchronously get the specified body part.
		/// </summary>
		/// <remarks>
		/// Gets the specified body part.
		/// </remarks>
		/// <returns>The body part.</returns>
		/// <param name="uid">The UID of the message.</param>
		/// <param name="partSpecifier">The body part specifier.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <param name="progress">The progress reporting mechanism.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <paramref name="partSpecifier"/> is <c>null</c>.
		/// </exception>
		/// <exception cref="System.ArgumentException">
		/// <paramref name="uid"/> is invalid.
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
		/// <exception cref="MessageNotFoundException">
		/// The IMAP server did not return the requested message body.
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
		public virtual Task<MimeEntity> GetBodyPartAsync (UniqueId uid, string partSpecifier, CancellationToken cancellationToken = default (CancellationToken), ITransferProgress progress = null)
		{
			return GetBodyPartAsync (uid, partSpecifier, true, cancellationToken, progress);
		}

		/// <summary>
		/// Get the specified body part.
		/// </summary>
		/// <remarks>
		/// Gets the specified body part.
		/// </remarks>
		/// <example>
		/// <code language="c#" source="Examples\ImapExamples.cs" region="DownloadBodyParts"/>
		/// </example>
		/// <returns>The body part.</returns>
		/// <param name="uid">The UID of the message.</param>
		/// <param name="part">The body part.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <param name="progress">The progress reporting mechanism.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <paramref name="part"/> is <c>null</c>.
		/// </exception>
		/// <exception cref="System.ArgumentException">
		/// <paramref name="uid"/> is invalid.
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
		/// <exception cref="MessageNotFoundException">
		/// The IMAP server did not return the requested message body.
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
		public override MimeEntity GetBodyPart (UniqueId uid, BodyPart part, CancellationToken cancellationToken = default (CancellationToken), ITransferProgress progress = null)
		{
			if (!uid.IsValid)
				throw new ArgumentException ("The uid is invalid.", nameof (uid));

			if (part == null)
				throw new ArgumentNullException (nameof (part));

			return GetBodyPart (uid, part.PartSpecifier, cancellationToken, progress);
		}

		/// <summary>
		/// Asynchronously get the specified body part.
		/// </summary>
		/// <remarks>
		/// Gets the specified body part.
		/// </remarks>
		/// <example>
		/// <code language="c#" source="Examples\ImapExamples.cs" region="DownloadBodyParts"/>
		/// </example>
		/// <returns>The body part.</returns>
		/// <param name="uid">The UID of the message.</param>
		/// <param name="part">The body part.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <param name="progress">The progress reporting mechanism.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <paramref name="part"/> is <c>null</c>.
		/// </exception>
		/// <exception cref="System.ArgumentException">
		/// <paramref name="uid"/> is invalid.
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
		/// <exception cref="MessageNotFoundException">
		/// The IMAP server did not return the requested message body.
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
		public override Task<MimeEntity> GetBodyPartAsync (UniqueId uid, BodyPart part, CancellationToken cancellationToken = default (CancellationToken), ITransferProgress progress = null)
		{
			if (!uid.IsValid)
				throw new ArgumentException ("The uid is invalid.", nameof (uid));

			if (part == null)
				throw new ArgumentNullException (nameof (part));

			return GetBodyPartAsync (uid, part.PartSpecifier, cancellationToken, progress);
		}

		async Task<MimeEntity> GetBodyPartAsync (int index, string partSpecifier, bool doAsync, CancellationToken cancellationToken, ITransferProgress progress)
		{
			if (index < 0 || index >= Count)
				throw new ArgumentOutOfRangeException (nameof (index));

			if (partSpecifier == null)
				throw new ArgumentNullException (nameof (partSpecifier));

			CheckState (true, false);

			string[] tags;

			var seqid = (index + 1).ToString (CultureInfo.InvariantCulture);
			var command = string.Format ("FETCH {0} ({1})\r\n", seqid, GetBodyPartQuery (partSpecifier, false, out tags));
			var ic = new ImapCommand (Engine, cancellationToken, this, command);
			var ctx = new FetchStreamContext (progress);
			ChainedStream chained = null;
			bool dispose = false;
			Section section;

			ic.RegisterUntaggedHandler ("FETCH", FetchStreamAsync);
			ic.UserData = ctx;

			Engine.QueueCommand (ic);

			try {
				await Engine.RunAsync (ic, doAsync).ConfigureAwait (false);

				ProcessResponseCodes (ic, null);

				if (ic.Response != ImapCommandResponse.Ok)
					throw ImapCommandException.Create ("FETCH", ic);

				chained = new ChainedStream ();

				foreach (var tag in tags) {
					if (!ctx.TryGetSection (index, tag, out section, true))
						throw new MessageNotFoundException ("The IMAP server did not return the requested body part.");

					if (!(section.Stream is MemoryStream || section.Stream is MemoryBlockStream))
						dispose = true;

					chained.Add (section.Stream);
				}
			} catch {
				if (chained != null)
					chained.Dispose ();

				throw;
			} finally {
				ctx.Dispose ();
			}

			var entity = await ParseEntityAsync (chained, dispose, doAsync, cancellationToken).ConfigureAwait (false);

			if (partSpecifier.Length == 0) {
				for (int i = entity.Headers.Count; i > 0; i--) {
					var header = entity.Headers[i - 1];

					if (!header.Field.StartsWith ("Content-", StringComparison.OrdinalIgnoreCase))
						entity.Headers.RemoveAt (i - 1);
				}
			}

			return entity;
		}

		/// <summary>
		/// Get the specified body part.
		/// </summary>
		/// <remarks>
		/// Gets the specified body part.
		/// </remarks>
		/// <returns>The body part.</returns>
		/// <param name="index">The index of the message.</param>
		/// <param name="partSpecifier">The body part specifier.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <param name="progress">The progress reporting mechanism.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <paramref name="partSpecifier"/> is <c>null</c>.
		/// </exception>
		/// <exception cref="System.ArgumentOutOfRangeException">
		/// <paramref name="index"/> is out of range.
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
		/// <exception cref="MessageNotFoundException">
		/// The IMAP server did not return the requested message.
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
		public virtual MimeEntity GetBodyPart (int index, string partSpecifier, CancellationToken cancellationToken = default (CancellationToken), ITransferProgress progress = null)
		{
			return GetBodyPartAsync (index, partSpecifier, false, cancellationToken, progress).GetAwaiter ().GetResult ();
		}

		/// <summary>
		/// Asynchronously get the specified body part.
		/// </summary>
		/// <remarks>
		/// Gets the specified body part.
		/// </remarks>
		/// <returns>The body part.</returns>
		/// <param name="index">The index of the message.</param>
		/// <param name="partSpecifier">The body part specifier.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <param name="progress">The progress reporting mechanism.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <paramref name="partSpecifier"/> is <c>null</c>.
		/// </exception>
		/// <exception cref="System.ArgumentOutOfRangeException">
		/// <paramref name="index"/> is out of range.
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
		/// <exception cref="MessageNotFoundException">
		/// The IMAP server did not return the requested message.
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
		public virtual Task<MimeEntity> GetBodyPartAsync (int index, string partSpecifier, CancellationToken cancellationToken = default (CancellationToken), ITransferProgress progress = null)
		{
			return GetBodyPartAsync (index, partSpecifier, true, cancellationToken, progress);
		}

		/// <summary>
		/// Get the specified body part.
		/// </summary>
		/// <remarks>
		/// Gets the specified body part.
		/// </remarks>
		/// <returns>The body part.</returns>
		/// <param name="index">The index of the message.</param>
		/// <param name="part">The body part.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <param name="progress">The progress reporting mechanism.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <paramref name="part"/> is <c>null</c>.
		/// </exception>
		/// <exception cref="System.ArgumentOutOfRangeException">
		/// <paramref name="index"/> is out of range.
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
		/// <exception cref="MessageNotFoundException">
		/// The IMAP server did not return the requested message.
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
		public override MimeEntity GetBodyPart (int index, BodyPart part, CancellationToken cancellationToken = default (CancellationToken), ITransferProgress progress = null)
		{
			if (index < 0 || index >= Count)
				throw new ArgumentOutOfRangeException (nameof (index));

			if (part == null)
				throw new ArgumentNullException (nameof (part));

			return GetBodyPart (index, part.PartSpecifier, cancellationToken, progress);
		}

		/// <summary>
		/// Asynchronously get the specified body part.
		/// </summary>
		/// <remarks>
		/// Gets the specified body part.
		/// </remarks>
		/// <returns>The body part.</returns>
		/// <param name="index">The index of the message.</param>
		/// <param name="part">The body part.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <param name="progress">The progress reporting mechanism.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <paramref name="part"/> is <c>null</c>.
		/// </exception>
		/// <exception cref="System.ArgumentOutOfRangeException">
		/// <paramref name="index"/> is out of range.
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
		/// <exception cref="MessageNotFoundException">
		/// The IMAP server did not return the requested message.
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
		public override Task<MimeEntity> GetBodyPartAsync (int index, BodyPart part, CancellationToken cancellationToken = default (CancellationToken), ITransferProgress progress = null)
		{
			if (index < 0 || index >= Count)
				throw new ArgumentOutOfRangeException (nameof (index));

			if (part == null)
				throw new ArgumentNullException (nameof (part));

			return GetBodyPartAsync (index, part.PartSpecifier, cancellationToken, progress);
		}

		async Task<Stream> GetStreamAsync (UniqueId uid, int offset, int count, bool doAsync, CancellationToken cancellationToken, ITransferProgress progress)
		{
			if (!uid.IsValid)
				throw new ArgumentException ("The uid is invalid.", nameof (uid));

			if (offset < 0)
				throw new ArgumentOutOfRangeException (nameof (offset));

			if (count < 0)
				throw new ArgumentOutOfRangeException (nameof (count));

			CheckState (true, false);

			if (count == 0)
				return new MemoryStream ();

			var ic = new ImapCommand (Engine, cancellationToken, this, "UID FETCH %u (BODY.PEEK[]<%d.%d>)\r\n", uid.Id, offset, count);
			var ctx = new FetchStreamContext (progress);
			Section section;

			ic.RegisterUntaggedHandler ("FETCH", FetchStreamAsync);
			ic.UserData = ctx;

			Engine.QueueCommand (ic);

			try {
				await Engine.RunAsync (ic, doAsync).ConfigureAwait (false);

				ProcessResponseCodes (ic, null);

				if (ic.Response != ImapCommandResponse.Ok)
					throw ImapCommandException.Create ("FETCH", ic);

				if (!ctx.TryGetSection (uid, string.Empty, out section, true))
					throw new MessageNotFoundException ("The IMAP server did not return the requested stream.");
			} finally {
				ctx.Dispose ();
			}

			return section.Stream;
		}

		async Task<Stream> GetStreamAsync (int index, int offset, int count, bool doAsync, CancellationToken cancellationToken, ITransferProgress progress)
		{
			if (index < 0 || index >= Count)
				throw new ArgumentOutOfRangeException (nameof (index));

			if (offset < 0)
				throw new ArgumentOutOfRangeException (nameof (offset));

			if (count < 0)
				throw new ArgumentOutOfRangeException (nameof (count));

			CheckState (true, false);

			if (count == 0)
				return new MemoryStream ();

			var ic = new ImapCommand (Engine, cancellationToken, this, "FETCH %d (BODY.PEEK[]<%d.%d>)\r\n", index + 1, offset, count);
			var ctx = new FetchStreamContext (progress);
			Section section;

			ic.RegisterUntaggedHandler ("FETCH", FetchStreamAsync);
			ic.UserData = ctx;

			Engine.QueueCommand (ic);

			try {
				await Engine.RunAsync (ic, doAsync).ConfigureAwait (false);

				ProcessResponseCodes (ic, null);

				if (ic.Response != ImapCommandResponse.Ok)
					throw ImapCommandException.Create ("FETCH", ic);

				if (!ctx.TryGetSection (index, string.Empty, out section, true))
					throw new MessageNotFoundException ("The IMAP server did not return the requested stream.");
			} finally {
				ctx.Dispose ();
			}

			return section.Stream;
		}

		/// <summary>
		/// Get a substream of the specified message.
		/// </summary>
		/// <remarks>
		/// Fetches a substream of the message. If the starting offset is beyond
		/// the end of the message, an empty stream is returned. If the number of
		/// bytes desired extends beyond the end of the message, a truncated stream
		/// will be returned.
		/// </remarks>
		/// <returns>The stream.</returns>
		/// <param name="uid">The UID of the message.</param>
		/// <param name="offset">The starting offset of the first desired byte.</param>
		/// <param name="count">The number of bytes desired.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <param name="progress">The progress reporting mechanism.</param>
		/// <exception cref="System.ArgumentException">
		/// <paramref name="uid"/> is invalid.
		/// </exception>
		/// <exception cref="System.ArgumentOutOfRangeException">
		/// <para><paramref name="offset"/> is negative.</para>
		/// <para>-or-</para>
		/// <para><paramref name="count"/> is negative.</para>
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
		/// <exception cref="MessageNotFoundException">
		/// The IMAP server did not return the requested message stream.
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
		public override Stream GetStream (UniqueId uid, int offset, int count, CancellationToken cancellationToken = default (CancellationToken), ITransferProgress progress = null)
		{
			return GetStreamAsync (uid, offset, count, false, cancellationToken, progress).GetAwaiter ().GetResult ();
		}

		/// <summary>
		/// Asynchronously gets a substream of the specified message.
		/// </summary>
		/// <remarks>
		/// Fetches a substream of the message. If the starting offset is beyond
		/// the end of the message, an empty stream is returned. If the number of
		/// bytes desired extends beyond the end of the message, a truncated stream
		/// will be returned.
		/// </remarks>
		/// <returns>The stream.</returns>
		/// <param name="uid">The UID of the message.</param>
		/// <param name="offset">The starting offset of the first desired byte.</param>
		/// <param name="count">The number of bytes desired.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <param name="progress">The progress reporting mechanism.</param>
		/// <exception cref="System.ArgumentException">
		/// <paramref name="uid"/> is invalid.
		/// </exception>
		/// <exception cref="System.ArgumentOutOfRangeException">
		/// <para><paramref name="offset"/> is negative.</para>
		/// <para>-or-</para>
		/// <para><paramref name="count"/> is negative.</para>
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
		/// <exception cref="MessageNotFoundException">
		/// The IMAP server did not return the requested message stream.
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
		public override Task<Stream> GetStreamAsync (UniqueId uid, int offset, int count, CancellationToken cancellationToken = default (CancellationToken), ITransferProgress progress = null)
		{
			return GetStreamAsync (uid, offset, count, true, cancellationToken, progress);
		}

		/// <summary>
		/// Get a substream of the specified message.
		/// </summary>
		/// <remarks>
		/// Fetches a substream of the message. If the starting offset is beyond
		/// the end of the message, an empty stream is returned. If the number of
		/// bytes desired extends beyond the end of the message, a truncated stream
		/// will be returned.
		/// </remarks>
		/// <returns>The stream.</returns>
		/// <param name="index">The index of the message.</param>
		/// <param name="offset">The starting offset of the first desired byte.</param>
		/// <param name="count">The number of bytes desired.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <param name="progress">The progress reporting mechanism.</param>
		/// <exception cref="System.ArgumentOutOfRangeException">
		/// <para><paramref name="index"/> is out of range.</para>
		/// <para>-or-</para>
		/// <para><paramref name="offset"/> is negative.</para>
		/// <para>-or-</para>
		/// <para><paramref name="count"/> is negative.</para>
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
		/// <exception cref="MessageNotFoundException">
		/// The IMAP server did not return the requested message stream.
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
		public override Stream GetStream (int index, int offset, int count, CancellationToken cancellationToken = default (CancellationToken), ITransferProgress progress = null)
		{
			return GetStreamAsync (index, offset, count, false, cancellationToken, progress).GetAwaiter ().GetResult ();
		}

		/// <summary>
		/// Asynchronously gets a substream of the specified message.
		/// </summary>
		/// <remarks>
		/// Fetches a substream of the message. If the starting offset is beyond
		/// the end of the message, an empty stream is returned. If the number of
		/// bytes desired extends beyond the end of the message, a truncated stream
		/// will be returned.
		/// </remarks>
		/// <returns>The stream.</returns>
		/// <param name="index">The index of the message.</param>
		/// <param name="offset">The starting offset of the first desired byte.</param>
		/// <param name="count">The number of bytes desired.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <param name="progress">The progress reporting mechanism.</param>
		/// <exception cref="System.ArgumentOutOfRangeException">
		/// <para><paramref name="index"/> is out of range.</para>
		/// <para>-or-</para>
		/// <para><paramref name="offset"/> is negative.</para>
		/// <para>-or-</para>
		/// <para><paramref name="count"/> is negative.</para>
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
		/// <exception cref="MessageNotFoundException">
		/// The IMAP server did not return the requested message stream.
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
		public override Task<Stream> GetStreamAsync (int index, int offset, int count, CancellationToken cancellationToken = default (CancellationToken), ITransferProgress progress = null)
		{
			return GetStreamAsync (index, offset, count, true, cancellationToken, progress);
		}

		async Task<Stream> GetStreamAsync (UniqueId uid, string section, bool doAsync, CancellationToken cancellationToken, ITransferProgress progress)
		{
			if (!uid.IsValid)
				throw new ArgumentException ("The uid is invalid.", nameof (uid));

			if (section == null)
				throw new ArgumentNullException (nameof (section));

			CheckState (true, false);

			var command = string.Format ("UID FETCH {0} (BODY.PEEK[{1}])\r\n", uid, section);
			var ic = new ImapCommand (Engine, cancellationToken, this, command);
			var ctx = new FetchStreamContext (progress);
			Section s;

			ic.RegisterUntaggedHandler ("FETCH", FetchStreamAsync);
			ic.UserData = ctx;

			Engine.QueueCommand (ic);

			try {
				await Engine.RunAsync (ic, doAsync).ConfigureAwait (false);

				ProcessResponseCodes (ic, null);

				if (ic.Response != ImapCommandResponse.Ok)
					throw ImapCommandException.Create ("FETCH", ic);

				if (!ctx.TryGetSection (uid, section, out s, true))
					throw new MessageNotFoundException ("The IMAP server did not return the requested stream.");
			} finally {
				ctx.Dispose ();
			}

			return s.Stream;
		}

		/// <summary>
		/// Get a substream of the specified body part.
		/// </summary>
		/// <remarks>
		/// <para>Gets a substream of the specified message.</para>
		/// <para>For more information about how to construct the <paramref name="section"/>,
		/// see Section 6.4.5 of RFC3501.</para>
		/// </remarks>
		/// <returns>The stream.</returns>
		/// <param name="uid">The UID of the message.</param>
		/// <param name="section">The desired section of the message.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <param name="progress">The progress reporting mechanism.</param>
		/// <exception cref="System.ArgumentException">
		/// <paramref name="uid"/> is invalid.
		/// </exception>
		/// <exception cref="System.ArgumentNullException">
		/// <paramref name="section"/> is <c>null</c>.
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
		/// <exception cref="MessageNotFoundException">
		/// The IMAP server did not return the requested message stream.
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
		public override Stream GetStream (UniqueId uid, string section, CancellationToken cancellationToken = default (CancellationToken), ITransferProgress progress = null)
		{
			return GetStreamAsync (uid, section, false, cancellationToken, progress).GetAwaiter ().GetResult ();
		}

		/// <summary>
		/// Asynchronously gets a substream of the specified body part.
		/// </summary>
		/// <remarks>
		/// <para>Gets a substream of the specified message.</para>
		/// <para>For more information about how to construct the <paramref name="section"/>,
		/// see Section 6.4.5 of RFC3501.</para>
		/// </remarks>
		/// <returns>The stream.</returns>
		/// <param name="uid">The UID of the message.</param>
		/// <param name="section">The desired section of the message.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <param name="progress">The progress reporting mechanism.</param>
		/// <exception cref="System.ArgumentException">
		/// <paramref name="uid"/> is invalid.
		/// </exception>
		/// <exception cref="System.ArgumentNullException">
		/// <paramref name="section"/> is <c>null</c>.
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
		/// <exception cref="MessageNotFoundException">
		/// The IMAP server did not return the requested message stream.
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
		public override Task<Stream> GetStreamAsync (UniqueId uid, string section, CancellationToken cancellationToken = default (CancellationToken), ITransferProgress progress = null)
		{
			return GetStreamAsync (uid, section, true, cancellationToken, progress);
		}

		async Task<Stream> GetStreamAsync (UniqueId uid, string section, int offset, int count, bool doAsync, CancellationToken cancellationToken, ITransferProgress progress)
		{
			if (!uid.IsValid)
				throw new ArgumentException ("The uid is invalid.", nameof (uid));

			if (section == null)
				throw new ArgumentNullException (nameof (section));

			if (offset < 0)
				throw new ArgumentOutOfRangeException (nameof (offset));

			if (count < 0)
				throw new ArgumentOutOfRangeException (nameof (count));

			CheckState (true, false);

			if (count == 0)
				return new MemoryStream ();

			var range = string.Format (CultureInfo.InvariantCulture, "{0}.{1}", offset, count);
			var command = string.Format ("UID FETCH {0} (BODY.PEEK[{1}]<{2}>)\r\n", uid, section, range);
			var ic = new ImapCommand (Engine, cancellationToken, this, command);
			var ctx = new FetchStreamContext (progress);
			Section s;

			ic.RegisterUntaggedHandler ("FETCH", FetchStreamAsync);
			ic.UserData = ctx;

			Engine.QueueCommand (ic);

			try {
				await Engine.RunAsync (ic, doAsync).ConfigureAwait (false);

				ProcessResponseCodes (ic, null);

				if (ic.Response != ImapCommandResponse.Ok)
					throw ImapCommandException.Create ("FETCH", ic);

				if (!ctx.TryGetSection (uid, section, out s, true))
					throw new MessageNotFoundException ("The IMAP server did not return the requested stream.");
			} finally {
				ctx.Dispose ();
			}

			return s.Stream;
		}

		/// <summary>
		/// Get a substream of the specified message.
		/// </summary>
		/// <remarks>
		/// <para>Gets a substream of the specified message. If the starting offset is beyond
		/// the end of the specified section of the message, an empty stream is returned. If
		/// the number of bytes desired extends beyond the end of the section, a truncated
		/// stream will be returned.</para>
		/// <para>For more information about how to construct the <paramref name="section"/>,
		/// see Section 6.4.5 of RFC3501.</para>
		/// </remarks>
		/// <returns>The stream.</returns>
		/// <param name="uid">The UID of the message.</param>
		/// <param name="section">The desired section of the message.</param>
		/// <param name="offset">The starting offset of the first desired byte.</param>
		/// <param name="count">The number of bytes desired.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <param name="progress">The progress reporting mechanism.</param>
		/// <exception cref="System.ArgumentException">
		/// <paramref name="uid"/> is invalid.
		/// </exception>
		/// <exception cref="System.ArgumentNullException">
		/// <paramref name="section"/> is <c>null</c>.
		/// </exception>
		/// <exception cref="System.ArgumentOutOfRangeException">
		/// <para><paramref name="offset"/> is negative.</para>
		/// <para>-or-</para>
		/// <para><paramref name="count"/> is negative.</para>
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
		/// <exception cref="MessageNotFoundException">
		/// The IMAP server did not return the requested message stream.
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
		public override Stream GetStream (UniqueId uid, string section, int offset, int count, CancellationToken cancellationToken = default (CancellationToken), ITransferProgress progress = null)
		{
			return GetStreamAsync (uid, section, offset, count, false, cancellationToken, progress).GetAwaiter ().GetResult ();
		}

		/// <summary>
		/// Asynchronously gets a substream of the specified message.
		/// </summary>
		/// <remarks>
		/// <para>Gets a substream of the specified message. If the starting offset is beyond
		/// the end of the specified section of the message, an empty stream is returned. If
		/// the number of bytes desired extends beyond the end of the section, a truncated
		/// stream will be returned.</para>
		/// <para>For more information about how to construct the <paramref name="section"/>,
		/// see Section 6.4.5 of RFC3501.</para>
		/// </remarks>
		/// <returns>The stream.</returns>
		/// <param name="uid">The UID of the message.</param>
		/// <param name="section">The desired section of the message.</param>
		/// <param name="offset">The starting offset of the first desired byte.</param>
		/// <param name="count">The number of bytes desired.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <param name="progress">The progress reporting mechanism.</param>
		/// <exception cref="System.ArgumentException">
		/// <paramref name="uid"/> is invalid.
		/// </exception>
		/// <exception cref="System.ArgumentNullException">
		/// <paramref name="section"/> is <c>null</c>.
		/// </exception>
		/// <exception cref="System.ArgumentOutOfRangeException">
		/// <para><paramref name="offset"/> is negative.</para>
		/// <para>-or-</para>
		/// <para><paramref name="count"/> is negative.</para>
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
		/// <exception cref="MessageNotFoundException">
		/// The IMAP server did not return the requested message stream.
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
		public override Task<Stream> GetStreamAsync (UniqueId uid, string section, int offset, int count, CancellationToken cancellationToken = default (CancellationToken), ITransferProgress progress = null)
		{
			return GetStreamAsync (uid, section, offset, count, true, cancellationToken, progress);
		}

		async Task<Stream> GetStreamAsync (int index, string section, bool doAsync, CancellationToken cancellationToken, ITransferProgress progress)
		{
			if (index < 0 || index >= Count)
				throw new ArgumentOutOfRangeException (nameof (index));

			if (section == null)
				throw new ArgumentNullException (nameof (section));

			CheckState (true, false);

			var seqid = (index + 1).ToString (CultureInfo.InvariantCulture);
			var command = string.Format ("FETCH {0} (BODY.PEEK[{1}])\r\n", seqid, section);
			var ic = new ImapCommand (Engine, cancellationToken, this, command);
			var ctx = new FetchStreamContext (progress);
			Section s;

			ic.RegisterUntaggedHandler ("FETCH", FetchStreamAsync);
			ic.UserData = ctx;

			Engine.QueueCommand (ic);

			try {
				await Engine.RunAsync (ic, doAsync).ConfigureAwait (false);

				ProcessResponseCodes (ic, null);

				if (ic.Response != ImapCommandResponse.Ok)
					throw ImapCommandException.Create ("FETCH", ic);

				if (!ctx.TryGetSection (index, section, out s, true))
					throw new MessageNotFoundException ("The IMAP server did not return the requested stream.");
			} finally {
				ctx.Dispose ();
			}

			return s.Stream;
		}

		/// <summary>
		/// Get a substream of the specified message.
		/// </summary>
		/// <remarks>
		/// <para>Gets a substream of the specified message.</para>
		/// <para>For more information about how to construct the <paramref name="section"/>,
		/// see Section 6.4.5 of RFC3501.</para>
		/// </remarks>
		/// <returns>The stream.</returns>
		/// <param name="index">The index of the message.</param>
		/// <param name="section">The desired section of the message.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <param name="progress">The progress reporting mechanism.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <paramref name="section"/> is <c>null</c>.
		/// </exception>
		/// <exception cref="System.ArgumentOutOfRangeException">
		/// <paramref name="index"/> is out of range.
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
		/// <exception cref="MessageNotFoundException">
		/// The IMAP server did not return the requested message stream.
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
		public override Stream GetStream (int index, string section, CancellationToken cancellationToken = default (CancellationToken), ITransferProgress progress = null)
		{
			return GetStreamAsync (index, section, false, cancellationToken, progress).GetAwaiter ().GetResult ();
		}

		/// <summary>
		/// Asynchronously gets a substream of the specified message.
		/// </summary>
		/// <remarks>
		/// <para>Gets a substream of the specified message.</para>
		/// <para>For more information about how to construct the <paramref name="section"/>,
		/// see Section 6.4.5 of RFC3501.</para>
		/// </remarks>
		/// <returns>The stream.</returns>
		/// <param name="index">The index of the message.</param>
		/// <param name="section">The desired section of the message.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <param name="progress">The progress reporting mechanism.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <paramref name="section"/> is <c>null</c>.
		/// </exception>
		/// <exception cref="System.ArgumentOutOfRangeException">
		/// <paramref name="index"/> is out of range.
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
		/// <exception cref="MessageNotFoundException">
		/// The IMAP server did not return the requested message stream.
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
		public override Task<Stream> GetStreamAsync (int index, string section, CancellationToken cancellationToken = default (CancellationToken), ITransferProgress progress = null)
		{
			return GetStreamAsync (index, section, true, cancellationToken, progress);
		}

		async Task<Stream> GetStreamAsync (int index, string section, int offset, int count, bool doAsync, CancellationToken cancellationToken, ITransferProgress progress)
		{
			if (index < 0 || index >= Count)
				throw new ArgumentOutOfRangeException (nameof (index));

			if (section == null)
				throw new ArgumentNullException (nameof (section));

			if (offset < 0)
				throw new ArgumentOutOfRangeException (nameof (offset));

			if (count < 0)
				throw new ArgumentOutOfRangeException (nameof (count));

			CheckState (true, false);

			if (count == 0)
				return new MemoryStream ();

			var seqid = (index + 1).ToString (CultureInfo.InvariantCulture);
			var range = string.Format (CultureInfo.InvariantCulture, "{0}.{1}", offset, count);
			var command = string.Format ("FETCH {0} (BODY.PEEK[{1}]<{2}>)\r\n", seqid, section, range);
			var ic = new ImapCommand (Engine, cancellationToken, this, command);
			var ctx = new FetchStreamContext (progress);
			Section s;

			ic.RegisterUntaggedHandler ("FETCH", FetchStreamAsync);
			ic.UserData = ctx;

			Engine.QueueCommand (ic);

			try {
				await Engine.RunAsync (ic, doAsync).ConfigureAwait (false);

				ProcessResponseCodes (ic, null);

				if (ic.Response != ImapCommandResponse.Ok)
					throw ImapCommandException.Create ("FETCH", ic);

				if (!ctx.TryGetSection (index, section, out s, true))
					throw new MessageNotFoundException ("The IMAP server did not return the requested stream.");
			} finally {
				ctx.Dispose ();
			}

			return s.Stream;
		}

		/// <summary>
		/// Get a substream of the specified message.
		/// </summary>
		/// <remarks>
		/// <para>Gets a substream of the specified message. If the starting offset is beyond
		/// the end of the specified section of the message, an empty stream is returned. If
		/// the number of bytes desired extends beyond the end of the section, a truncated
		/// stream will be returned.</para>
		/// <para>For more information about how to construct the <paramref name="section"/>,
		/// see Section 6.4.5 of RFC3501.</para>
		/// </remarks>
		/// <returns>The stream.</returns>
		/// <param name="index">The index of the message.</param>
		/// <param name="section">The desired section of the message.</param>
		/// <param name="offset">The starting offset of the first desired byte.</param>
		/// <param name="count">The number of bytes desired.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <param name="progress">The progress reporting mechanism.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <paramref name="section"/> is <c>null</c>.
		/// </exception>
		/// <exception cref="System.ArgumentOutOfRangeException">
		/// <para><paramref name="index"/> is out of range.</para>
		/// <para>-or-</para>
		/// <para><paramref name="offset"/> is negative.</para>
		/// <para>-or-</para>
		/// <para><paramref name="count"/> is negative.</para>
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
		/// <exception cref="MessageNotFoundException">
		/// The IMAP server did not return the requested message stream.
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
		public override Stream GetStream (int index, string section, int offset, int count, CancellationToken cancellationToken = default (CancellationToken), ITransferProgress progress = null)
		{
			return GetStreamAsync (index, section, offset, count, false, cancellationToken, progress).GetAwaiter ().GetResult ();
		}

		/// <summary>
		/// Asynchronously gets a substream of the specified message.
		/// </summary>
		/// <remarks>
		/// <para>Gets a substream of the specified message. If the starting offset is beyond
		/// the end of the specified section of the message, an empty stream is returned. If
		/// the number of bytes desired extends beyond the end of the section, a truncated
		/// stream will be returned.</para>
		/// <para>For more information about how to construct the <paramref name="section"/>,
		/// see Section 6.4.5 of RFC3501.</para>
		/// </remarks>
		/// <returns>The stream.</returns>
		/// <param name="index">The index of the message.</param>
		/// <param name="section">The desired section of the message.</param>
		/// <param name="offset">The starting offset of the first desired byte.</param>
		/// <param name="count">The number of bytes desired.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <param name="progress">The progress reporting mechanism.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <paramref name="section"/> is <c>null</c>.
		/// </exception>
		/// <exception cref="System.ArgumentOutOfRangeException">
		/// <para><paramref name="index"/> is out of range.</para>
		/// <para>-or-</para>
		/// <para><paramref name="offset"/> is negative.</para>
		/// <para>-or-</para>
		/// <para><paramref name="count"/> is negative.</para>
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
		/// <exception cref="MessageNotFoundException">
		/// The IMAP server did not return the requested message stream.
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
		public override Task<Stream> GetStreamAsync (int index, string section, int offset, int count, CancellationToken cancellationToken = default (CancellationToken), ITransferProgress progress = null)
		{
			return GetStreamAsync (index, section, offset, count, true, cancellationToken, progress);
		}

		class FetchStreamCallbackContext : FetchStreamContextBase
		{
			readonly ImapFolder folder;
			readonly object callback;

			public FetchStreamCallbackContext (ImapFolder folder, object callback, ITransferProgress progress) : base (progress)
			{
				this.folder = folder;
				this.callback = callback;
			}

			Task InvokeCallbackAsync (ImapFolder folder, int index, UniqueId uid, Stream stream, bool doAsync, CancellationToken cancellationToken)
			{
				if (doAsync)
					return ((ImapFetchStreamAsyncCallback) callback) (folder, index, uid, stream, cancellationToken);

				((ImapFetchStreamCallback) callback) (folder, index, uid, stream);
				return Complete;
			}

			public override async Task AddAsync (Section section, bool doAsync, CancellationToken cancellationToken)
			{
				if (section.UniqueId.HasValue) {
					await InvokeCallbackAsync (folder, section.Index, section.UniqueId.Value, section.Stream, doAsync, cancellationToken).ConfigureAwait (false);
					section.Stream.Dispose ();
				} else {
					Sections.Add (section);
				}
			}

			public override async Task SetUniqueIdAsync (int index, UniqueId uid, bool doAsync, CancellationToken cancellationToken)
			{
				for (int i = 0; i < Sections.Count; i++) {
					if (Sections[i].Index == index) {
						await InvokeCallbackAsync (folder, index, uid, Sections[i].Stream, doAsync, cancellationToken).ConfigureAwait (false);
						Sections[i].Stream.Dispose ();
						Sections.RemoveAt (i);
						break;
					}
				}
			}
		}

		async Task GetStreamsAsync (IList<UniqueId> uids, object callback, bool doAsync, CancellationToken cancellationToken, ITransferProgress progress)
		{
			if (uids == null)
				throw new ArgumentNullException (nameof (uids));

			if (callback == null)
				throw new ArgumentNullException (nameof (callback));

			CheckState (true, false);

			if (uids.Count == 0)
				return;

			var ctx = new FetchStreamCallbackContext (this, callback, progress);
			var command = "UID FETCH %s (BODY.PEEK[])\r\n";

			try {
				foreach (var ic in Engine.CreateCommands (cancellationToken, this, command, uids)) {
					ic.RegisterUntaggedHandler ("FETCH", FetchStreamAsync);
					ic.UserData = ctx;

					Engine.QueueCommand (ic);

					await Engine.RunAsync (ic, doAsync).ConfigureAwait (false);

					ProcessResponseCodes (ic, null);

					if (ic.Response != ImapCommandResponse.Ok)
						throw ImapCommandException.Create ("FETCH", ic);
				}
			} finally {
				ctx.Dispose ();
			}
		}

		async Task GetStreamsAsync (IList<int> indexes, object callback, bool doAsync, CancellationToken cancellationToken, ITransferProgress progress)
		{
			if (indexes == null)
				throw new ArgumentNullException (nameof (indexes));

			if (callback == null)
				throw new ArgumentNullException (nameof (callback));

			CheckState (true, false);
			CheckAllowIndexes ();

			if (indexes.Count == 0)
				return;

			var set = ImapUtils.FormatIndexSet (indexes);
			var command = string.Format ("FETCH {0} (UID BODY.PEEK[])\r\n", set);
			var ic = new ImapCommand (Engine, cancellationToken, this, command);
			var ctx = new FetchStreamCallbackContext (this, callback, progress);

			ic.RegisterUntaggedHandler ("FETCH", FetchStreamAsync);
			ic.UserData = ctx;

			Engine.QueueCommand (ic);

			try {
				await Engine.RunAsync (ic, doAsync).ConfigureAwait (false);

				ProcessResponseCodes (ic, null);

				if (ic.Response != ImapCommandResponse.Ok)
					throw ImapCommandException.Create ("FETCH", ic);
			} finally {
				ctx.Dispose ();
			}
		}

		async Task GetStreamsAsync (int min, int max, object callback, bool doAsync, CancellationToken cancellationToken, ITransferProgress progress)
		{
			if (min < 0)
				throw new ArgumentOutOfRangeException (nameof (min));

			if (max != -1 && max < min)
				throw new ArgumentOutOfRangeException (nameof (max));

			if (callback == null)
				throw new ArgumentNullException (nameof (callback));

			CheckState (true, false);
			CheckAllowIndexes ();

			if (min == Count)
				return;

			var command = string.Format ("FETCH {0} (UID BODY.PEEK[])\r\n", GetFetchRange (min, max));
			var ic = new ImapCommand (Engine, cancellationToken, this, command);
			var ctx = new FetchStreamCallbackContext (this, callback, progress);

			ic.RegisterUntaggedHandler ("FETCH", FetchStreamAsync);
			ic.UserData = ctx;

			Engine.QueueCommand (ic);

			try {
				await Engine.RunAsync (ic, doAsync).ConfigureAwait (false);

				ProcessResponseCodes (ic, null);

				if (ic.Response != ImapCommandResponse.Ok)
					throw ImapCommandException.Create ("FETCH", ic);
			} finally {
				ctx.Dispose ();
			}
		}

		/// <summary>
		/// Get the streams for the specified messages.
		/// </summary>
		/// <remarks>
		/// <para>Gets the streams for the specified messages.</para>
		/// </remarks>
		/// <param name="uids">The uids of the messages.</param>
		/// <param name="callback"></param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <param name="progress">The progress reporting mechanism.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <para><paramref name="uids"/> is <c>null</c>.</para>
		/// <para>-or-</para>
		/// <para><paramref name="callback"/> is <c>null</c>.</para>
		/// </exception>
		/// <exception cref="System.ArgumentException">
		/// One or more of the <paramref name="uids"/> is invalid.
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
		public virtual void GetStreams (IList<UniqueId> uids, ImapFetchStreamCallback callback, CancellationToken cancellationToken = default (CancellationToken), ITransferProgress progress = null)
		{
			GetStreamsAsync (uids, callback, false, cancellationToken, progress).GetAwaiter ().GetResult ();
		}

		/// <summary>
		/// Asynchronously get the streams for the specified messages.
		/// </summary>
		/// <remarks>
		/// <para>Asynchronously gets the streams for the specified messages.</para>
		/// </remarks>
		/// <returns>An awaitable task.</returns>
		/// <param name="uids">The uids of the messages.</param>
		/// <param name="callback"></param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <param name="progress">The progress reporting mechanism.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <para><paramref name="uids"/> is <c>null</c>.</para>
		/// <para>-or-</para>
		/// <para><paramref name="callback"/> is <c>null</c>.</para>
		/// </exception>
		/// <exception cref="System.ArgumentException">
		/// One or more of the <paramref name="uids"/> is invalid.
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
		public virtual Task GetStreamsAsync (IList<UniqueId> uids, ImapFetchStreamAsyncCallback callback, CancellationToken cancellationToken = default (CancellationToken), ITransferProgress progress = null)
		{
			return GetStreamsAsync (uids, callback, true, cancellationToken, progress);
		}

		/// <summary>
		/// Get the streams for the specified messages.
		/// </summary>
		/// <remarks>
		/// <para>Gets the streams for the specified messages.</para>
		/// </remarks>
		/// <param name="indexes">The indexes of the messages.</param>
		/// <param name="callback"></param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <param name="progress">The progress reporting mechanism.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <para><paramref name="indexes"/> is <c>null</c>.</para>
		/// <para>-or-</para>
		/// <para><paramref name="callback"/> is <c>null</c>.</para>
		/// </exception>
		/// <exception cref="System.ArgumentException">
		/// One or more of the <paramref name="indexes"/> is invalid.
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
		public virtual void GetStreams (IList<int> indexes, ImapFetchStreamCallback callback, CancellationToken cancellationToken = default (CancellationToken), ITransferProgress progress = null)
		{
			GetStreamsAsync (indexes, callback, false, cancellationToken, progress).GetAwaiter ().GetResult ();
		}

		/// <summary>
		/// Asynchronously get the streams for the specified messages.
		/// </summary>
		/// <remarks>
		/// <para>Asynchronously gets the streams for the specified messages.</para>
		/// </remarks>
		/// <returns>An awaitable task.</returns>
		/// <param name="indexes">The indexes of the messages.</param>
		/// <param name="callback"></param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <param name="progress">The progress reporting mechanism.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <para><paramref name="indexes"/> is <c>null</c>.</para>
		/// <para>-or-</para>
		/// <para><paramref name="callback"/> is <c>null</c>.</para>
		/// </exception>
		/// <exception cref="System.ArgumentException">
		/// One or more of the <paramref name="indexes"/> is invalid.
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
		public virtual Task GetStreamsAsync (IList<int> indexes, ImapFetchStreamAsyncCallback callback, CancellationToken cancellationToken = default (CancellationToken), ITransferProgress progress = null)
		{
			return GetStreamsAsync (indexes, callback, true, cancellationToken, progress);
		}

		/// <summary>
		/// Get the streams for the specified messages.
		/// </summary>
		/// <remarks>
		/// <para>Gets the streams for the specified messages.</para>
		/// </remarks>
		/// <param name="min">The minimum index.</param>
		/// <param name="max">The maximum index, or <c>-1</c> to specify no upper bound.</param>
		/// <param name="callback"></param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <param name="progress">The progress reporting mechanism.</param>
		/// <exception cref="System.ArgumentOutOfRangeException">
		/// <para><paramref name="min"/> is out of range.</para>
		/// <para>-or-</para>
		/// <para><paramref name="max"/> is out of range.</para>
		/// </exception>
		/// <exception cref="System.ArgumentNullException">
		/// <paramref name="callback"/> is <c>null</c>.
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
		public virtual void GetStreams (int min, int max, ImapFetchStreamCallback callback, CancellationToken cancellationToken = default (CancellationToken), ITransferProgress progress = null)
		{
			GetStreamsAsync (min, max, callback, false, cancellationToken, progress).GetAwaiter ().GetResult ();
		}

		/// <summary>
		/// Asynchronously get the streams for the specified messages.
		/// </summary>
		/// <remarks>
		/// <para>Asynchronously gets the streams for the specified messages.</para>
		/// </remarks>
		/// <returns>An awaitable task.</returns>
		/// <param name="min">The minimum index.</param>
		/// <param name="max">The maximum index, or <c>-1</c> to specify no upper bound.</param>
		/// <param name="callback"></param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <param name="progress">The progress reporting mechanism.</param>
		/// <exception cref="System.ArgumentOutOfRangeException">
		/// <para><paramref name="min"/> is out of range.</para>
		/// <para>-or-</para>
		/// <para><paramref name="max"/> is out of range.</para>
		/// </exception>
		/// <exception cref="System.ArgumentNullException">
		/// <paramref name="callback"/> is <c>null</c>.
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
		public virtual Task GetStreamsAsync (int min, int max, ImapFetchStreamAsyncCallback callback, CancellationToken cancellationToken = default (CancellationToken), ITransferProgress progress = null)
		{
			return GetStreamsAsync (min, max, callback, true, cancellationToken, progress);
		}
	}
}
