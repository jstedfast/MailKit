//
// ImapEngine.cs
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
using System.Diagnostics;
using System.Globalization;
using System.Threading.Tasks;
using System.Collections.Generic;

using MimeKit;

namespace MailKit.Net.Imap {
	delegate ImapFolder CreateImapFolderDelegate (ImapFolderConstructorArgs args);

	/// <summary>
	/// The state of the <see cref="ImapEngine"/>.
	/// </summary>
	enum ImapEngineState {
		/// <summary>
		/// The ImapEngine is in the disconnected state.
		/// </summary>
		Disconnected,

		/// <summary>
		/// The ImapEngine is in the process of connecting.
		/// </summary>
		Connecting,

		/// <summary>
		/// The ImapEngine is connected but not yet authenticated.
		/// </summary>
		Connected,

		/// <summary>
		/// The ImapEngine is in the authenticated state.
		/// </summary>
		Authenticated,

		/// <summary>
		/// The ImapEngine is in the selected state.
		/// </summary>
		Selected,

		/// <summary>
		/// The ImapEngine is in the IDLE state.
		/// </summary>
		Idle
	}

	enum ImapProtocolVersion {
		Unknown,
		IMAP4,
		IMAP4rev1
	}

	enum ImapUntaggedResult {
		Ok,
		No,
		Bad,
		Handled
	}

	enum ImapQuirksMode {
		None,
		Courier,
		Cyrus,
		Domino,
		Dovecot,
		Exchange,
		GMail,
		ProtonMail,
		SmarterMail,
		SunMicrosystems,
		UW,
		Yahoo,
		Yandex
	}

	class ImapFolderNameComparer : IEqualityComparer<string>
	{
		public char DirectorySeparator;

		public ImapFolderNameComparer (char directorySeparator)
		{
			DirectorySeparator = directorySeparator;
		}

		public bool Equals (string x, string y)
		{
			x = ImapUtils.CanonicalizeMailboxName (x, DirectorySeparator);
			y = ImapUtils.CanonicalizeMailboxName (y, DirectorySeparator);

			return x == y;
		}

		public int GetHashCode (string obj)
		{
			return ImapUtils.CanonicalizeMailboxName (obj, DirectorySeparator).GetHashCode ();
		}
	}

	/// <summary>
	/// An IMAP command engine.
	/// </summary>
	class ImapEngine : IDisposable
	{
		internal const string GenericUntaggedResponseSyntaxErrorFormat = "Syntax error in untagged {0} response. {1}";
		internal const string GenericItemSyntaxErrorFormat = "Syntax error in {0}. {1}";
		internal const string FetchBodySyntaxErrorFormat = "Syntax error in BODY. {0}";
		const string GenericResponseCodeSyntaxErrorFormat = "Syntax error in {0} response code. {1}";
		const string GreetingSyntaxErrorFormat = "Syntax error in IMAP server greeting. {0}";
		const int BufferSize = 4096;

		internal static readonly Encoding Latin1;
		internal static readonly Encoding UTF8;
		static int TagPrefixIndex;

		internal readonly Dictionary<string, ImapFolder> FolderCache;
		readonly CreateImapFolderDelegate createImapFolder;
		readonly ImapFolderNameComparer cacheComparer;
		internal ImapQuirksMode QuirksMode;
		readonly List<ImapCommand> queue;
		internal char TagPrefix;
		ImapCommand current;
		MimeParser parser;
		internal int Tag;
		bool disposed;

		static ImapEngine ()
		{
			UTF8 = Encoding.GetEncoding (65001, new EncoderExceptionFallback (), new DecoderExceptionFallback ());

			try {
				Latin1 = Encoding.GetEncoding (28591);
			} catch (NotSupportedException) {
				Latin1 = Encoding.GetEncoding (1252);
			}
		}

		public ImapEngine (CreateImapFolderDelegate createImapFolderDelegate)
		{
			cacheComparer = new ImapFolderNameComparer ('.');

			FolderCache = new Dictionary<string, ImapFolder> (cacheComparer);
			ThreadingAlgorithms = new HashSet<ThreadingAlgorithm> ();
			AuthenticationMechanisms = new HashSet<string> (StringComparer.Ordinal);
			CompressionAlgorithms = new HashSet<string> (StringComparer.Ordinal);
			SupportedContexts = new HashSet<string> (StringComparer.Ordinal);
			SupportedCharsets = new HashSet<string> (StringComparer.OrdinalIgnoreCase);
			Rights = new AccessRights ();

			PersonalNamespaces = new FolderNamespaceCollection ();
			SharedNamespaces = new FolderNamespaceCollection ();
			OtherNamespaces = new FolderNamespaceCollection ();

			ProtocolVersion = ImapProtocolVersion.Unknown;
			createImapFolder = createImapFolderDelegate;
			Capabilities = ImapCapabilities.None;
			QuirksMode = ImapQuirksMode.None;
			queue = new List<ImapCommand> ();
		}

		/// <summary>
		/// Get the authentication mechanisms supported by the IMAP server.
		/// </summary>
		/// <remarks>
		/// The authentication mechanisms are queried durring the
		/// <see cref="ConnectAsync"/> method.
		/// </remarks>
		/// <value>The authentication mechanisms.</value>
		public HashSet<string> AuthenticationMechanisms {
			get; private set;
		}

		/// <summary>
		/// Get the compression algorithms supported by the IMAP server.
		/// </summary>
		/// <remarks>
		/// The compression algorithms are populated by the
		/// <see cref="QueryCapabilitiesAsync"/> method.
		/// </remarks>
		/// <value>The compression algorithms.</value>
		public HashSet<string> CompressionAlgorithms {
			get; private set;
		}

		/// <summary>
		/// Get the threading algorithms supported by the IMAP server.
		/// </summary>
		/// <remarks>
		/// The threading algorithms are populated by the
		/// <see cref="QueryCapabilitiesAsync"/> method.
		/// </remarks>
		/// <value>The threading algorithms.</value>
		public HashSet<ThreadingAlgorithm> ThreadingAlgorithms {
			get; private set;
		}

		/// <summary>
		/// Gets the append limit supported by the IMAP server.
		/// </summary>
		/// <remarks>
		/// Gets the append limit supported by the IMAP server.
		/// </remarks>
		/// <value>The append limit.</value>
		public uint? AppendLimit {
			get; private set;
		}

		/// <summary>
		/// Gets the I18NLEVEL supported by the IMAP server.
		/// </summary>
		/// <remarks>
		/// Gets the I18NLEVEL supported by the IMAP server.
		/// </remarks>
		/// <value>The internationalization level.</value>
		public int I18NLevel {
			get; private set;
		}

		/// <summary>
		/// Get the capabilities supported by the IMAP server.
		/// </summary>
		/// <remarks>
		/// The capabilities will not be known until a successful connection
		/// has been made via the <see cref="ConnectAsync"/> method.
		/// </remarks>
		/// <value>The capabilities.</value>
		public ImapCapabilities Capabilities {
			get; set;
		}

		/// <summary>
		/// Indicates whether or not the engine is busy processing commands.
		/// </summary>
		/// <remarks>
		/// Indicates whether or not the engine is busy processing commands.
		/// </remarks>
		/// <value><c>true</c> if th e engine is busy processing commands; otherwise, <c>false</c>.</value>
		internal bool IsBusy {
			get { return current != null; }
		}

		/// <summary>
		/// Get the capabilities version.
		/// </summary>
		/// <remarks>
		/// Every time the engine receives an untagged CAPABILITIES
		/// response from the server, it increments this value.
		/// </remarks>
		/// <value>The capabilities version.</value>
		public int CapabilitiesVersion {
			get; private set;
		}

		/// <summary>
		/// Get the IMAP protocol version.
		/// </summary>
		/// <remarks>
		/// Gets the IMAP protocol version.
		/// </remarks>
		/// <value>The IMAP protocol version.</value>
		public ImapProtocolVersion ProtocolVersion {
			get; private set;
		}

		/// <summary>
		/// Get the rights specified in the capabilities.
		/// </summary>
		/// <remarks>
		/// Gets the rights specified in the capabilities.
		/// </remarks>
		/// <value>The rights.</value>
		public AccessRights Rights {
			get; private set;
		}

		/// <summary>
		/// Get the supported charsets.
		/// </summary>
		/// <remarks>
		/// Gets the supported charsets.
		/// </remarks>
		/// <value>The supported charsets.</value>
		public HashSet<string> SupportedCharsets {
			get; private set;
		}

		/// <summary>
		/// Get the supported contexts.
		/// </summary>
		/// <remarks>
		/// Gets the supported contexts.
		/// </remarks>
		/// <value>The supported contexts.</value>
		public HashSet<string> SupportedContexts {
			get; private set;
		}

		/// <summary>
		/// Get whether or not the QRESYNC feature has been enabled.
		/// </summary>
		/// <remarks>
		/// Gets whether or not the QRESYNC feature has been enabled.
		/// </remarks>
		/// <value><c>true</c> if the QRESYNC feature has been enabled; otherwise, <c>false</c>.</value>
		public bool QResyncEnabled {
			get; internal set;
		}

		/// <summary>
		/// Get whether or not the UTF8=ACCEPT feature has been enabled.
		/// </summary>
		/// <remarks>
		/// Gets whether or not the UTF8=ACCEPT feature has been enabled.
		/// </remarks>
		/// <value><c>true</c> if the UTF8=ACCEPT feature has been enabled; otherwise, <c>false</c>.</value>
		public bool UTF8Enabled {
			get; internal set;
		}

		/// <summary>
		/// Get the URI of the IMAP server.
		/// </summary>
		/// <remarks>
		/// Gets the URI of the IMAP server.
		/// </remarks>
		/// <value>The URI of the IMAP server.</value>
		public Uri Uri {
			get; internal set;
		}

		/// <summary>
		/// Get the underlying IMAP stream.
		/// </summary>
		/// <remarks>
		/// Gets the underlying IMAP stream.
		/// </remarks>
		/// <value>The IMAP stream.</value>
		public ImapStream Stream {
			get; private set;
		}

		/// <summary>
		/// Get or sets the state of the engine.
		/// </summary>
		/// <remarks>
		/// Gets or sets the state of the engine.
		/// </remarks>
		/// <value>The engine state.</value>
		public ImapEngineState State {
			get; internal set;
		}

		/// <summary>
		/// Get whether or not the engine is currently connected to a IMAP server.
		/// </summary>
		/// <remarks>
		/// Gets whether or not the engine is currently connected to a IMAP server.
		/// </remarks>
		/// <value><c>true</c> if the engine is connected; otherwise, <c>false</c>.</value>
		public bool IsConnected {
			get { return Stream != null && Stream.IsConnected; }
		}

		/// <summary>
		/// Gets the personal folder namespaces.
		/// </summary>
		/// <remarks>
		/// Gets the personal folder namespaces.
		/// </remarks>
		/// <value>The personal folder namespaces.</value>
		public FolderNamespaceCollection PersonalNamespaces {
			get; private set;
		}

		/// <summary>
		/// Gets the shared folder namespaces.
		/// </summary>
		/// <remarks>
		/// Gets the shared folder namespaces.
		/// </remarks>
		/// <value>The shared folder namespaces.</value>
		public FolderNamespaceCollection SharedNamespaces {
			get; private set;
		}

		/// <summary>
		/// Gets the other folder namespaces.
		/// </summary>
		/// <remarks>
		/// Gets the other folder namespaces.
		/// </remarks>
		/// <value>The other folder namespaces.</value>
		public FolderNamespaceCollection OtherNamespaces {
			get; private set;
		}

		/// <summary>
		/// Gets the selected folder.
		/// </summary>
		/// <remarks>
		/// Gets the selected folder.
		/// </remarks>
		/// <value>The selected folder.</value>
		public ImapFolder Selected {
			get; internal set;
		}

		/// <summary>
		/// Gets a value indicating whether the engine is disposed.
		/// </summary>
		/// <remarks>
		/// Gets a value indicating whether the engine is disposed.
		/// </remarks>
		/// <value><c>true</c> if the engine is disposed; otherwise, <c>false</c>.</value>
		public bool IsDisposed {
			get { return disposed; }
		}

		/// <summary>
		/// Gets whether the current NOTIFY status prevents using indexes and * for referencing messages.
		/// </summary>
		/// <remarks>
		/// Gets whether the current NOTIFY status prevents using indexes and * for referencing messages. This is the case when the client has asked for MessageNew or MessageExpunge events on the SELECTED mailbox.
		/// </remarks>
		/// <value><c>true</c> if the use of indexes and * is prevented; otherwise, <c>false</c>.</value>
		internal bool NotifySelectedNewExpunge {
			get; set;
		}

		#region Special Folders

		/// <summary>
		/// Gets the Inbox folder.
		/// </summary>
		/// <value>The Inbox folder.</value>
		public ImapFolder Inbox {
			get; private set;
		}

		/// <summary>
		/// Gets the special folder containing an aggregate of all messages.
		/// </summary>
		/// <value>The folder containing all messages.</value>
		public ImapFolder All {
			get; private set;
		}

		/// <summary>
		/// Gets the special archive folder.
		/// </summary>
		/// <value>The archive folder.</value>
		public ImapFolder Archive {
			get; private set;
		}

		/// <summary>
		/// Gets the special folder containing drafts.
		/// </summary>
		/// <value>The drafts folder.</value>
		public ImapFolder Drafts {
			get; private set;
		}

		/// <summary>
		/// Gets the special folder containing flagged messages.
		/// </summary>
		/// <value>The flagged folder.</value>
		public ImapFolder Flagged {
			get; private set;
		}

		/// <summary>
		/// Gets the special folder containing important messages.
		/// </summary>
		/// <value>The important folder.</value>
		public ImapFolder Important {
			get; private set;
		}

		/// <summary>
		/// Gets the special folder containing junk messages.
		/// </summary>
		/// <value>The junk folder.</value>
		public ImapFolder Junk {
			get; private set;
		}

		/// <summary>
		/// Gets the special folder containing sent messages.
		/// </summary>
		/// <value>The sent.</value>
		public ImapFolder Sent {
			get; private set;
		}

		/// <summary>
		/// Gets the folder containing deleted messages.
		/// </summary>
		/// <value>The trash folder.</value>
		public ImapFolder Trash {
			get; private set;
		}

		#endregion

		internal ImapFolder CreateImapFolder (string encodedName, FolderAttributes attributes, char delim)
		{
			var args = new ImapFolderConstructorArgs (this, encodedName, attributes, delim);

			return createImapFolder (args);
		}

		internal static ImapProtocolException UnexpectedToken (string format, params object[] args)
		{
			for (int i = 0; i < args.Length; i++) {
				if (args[i] is ImapToken token) {
					switch (token.Type) {
					case ImapTokenType.Atom: args[i] = string.Format ("Unexpected atom token: {0}", token); break;
					case ImapTokenType.Flag: args[i] = string.Format ("Unexpected flag token: {0}", token); break;
					case ImapTokenType.QString: args[i] = string.Format ("Unexpected qstring token: {0}", token); break;
					case ImapTokenType.Literal: args[i] = string.Format ("Unexpected literal token: {0}", token); break;
					default: args[i] = string.Format ("Unexpected token: {0}", token); break;
					}
					break;
				}
			}

			return new ImapProtocolException (string.Format (CultureInfo.InvariantCulture, format, args)) { UnexpectedToken = true };
		}

		internal static void AssertToken (ImapToken token, ImapTokenType type, string format, params object[] args)
		{
			if (token.Type != type)
				throw UnexpectedToken (format, args);
		}

		internal static void AssertToken (ImapToken token, ImapTokenType type1, ImapTokenType type2, string format, params object[] args)
		{
			if (token.Type != type1 && token.Type != type2)
				throw UnexpectedToken (format, args);
		}

		internal static uint ParseNumber (ImapToken token, bool nonZero, string format, params object[] args)
		{
			uint value;

			AssertToken (token, ImapTokenType.Atom, format, args);

			if (!uint.TryParse ((string) token.Value, NumberStyles.None, CultureInfo.InvariantCulture, out value) || (nonZero && value == 0))
				throw UnexpectedToken (format, args);

			return value;
		}

		internal static ulong ParseNumber64 (ImapToken token, bool nonZero, string format, params object[] args)
		{
			ulong value;

			AssertToken (token, ImapTokenType.Atom, format, args);

			if (!ulong.TryParse ((string) token.Value, NumberStyles.None, CultureInfo.InvariantCulture, out value) || (nonZero && value == 0))
				throw UnexpectedToken (format, args);

			return value;
		}

		internal static UniqueIdSet ParseUidSet (ImapToken token, uint validity, string format, params object[] args)
		{
			UniqueIdSet uids;

			AssertToken (token, ImapTokenType.Atom, format, args);

			if (!UniqueIdSet.TryParse ((string) token.Value, validity, out uids))
				throw UnexpectedToken (format, args);

			return uids;
		}

		/// <summary>
		/// Sets the stream - this is only here to be used by the unit tests.
		/// </summary>
		/// <param name="stream">The IMAP stream.</param>
		internal void SetStream (ImapStream stream)
		{
			Stream = stream;
		}

		/// <summary>
		/// Takes posession of the <see cref="ImapStream"/> and reads the greeting.
		/// </summary>
		/// <param name="stream">The IMAP stream.</param>
		/// <param name="doAsync">Whether or not asyncrhonois IO methods should be used.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.OperationCanceledException">
		/// The operation was canceled via the cancellation token.
		/// </exception>
		/// <exception cref="System.IO.IOException">
		/// An I/O error occurred.
		/// </exception>
		/// <exception cref="ImapProtocolException">
		/// An IMAP protocol error occurred.
		/// </exception>
		public async Task ConnectAsync (ImapStream stream, bool doAsync, CancellationToken cancellationToken)
		{
			TagPrefix = (char) ('A' + (TagPrefixIndex++ % 26));
			ProtocolVersion = ImapProtocolVersion.Unknown;
			Capabilities = ImapCapabilities.None;
			AuthenticationMechanisms.Clear ();
			CompressionAlgorithms.Clear ();
			ThreadingAlgorithms.Clear ();
			SupportedCharsets.Clear ();
			SupportedContexts.Clear ();
			Rights.Clear ();

			State = ImapEngineState.Connecting;
			QuirksMode = ImapQuirksMode.None;
			SupportedCharsets.Add ("US-ASCII");
			SupportedCharsets.Add ("UTF-8");
			CapabilitiesVersion = 0;
			QResyncEnabled = false;
			UTF8Enabled = false;
			AppendLimit = null;
			Selected = null;
			Stream = stream;
			I18NLevel = 0;
			Tag = 0;

			try {
				var token = await ReadTokenAsync (doAsync, cancellationToken).ConfigureAwait (false);

				AssertToken (token, ImapTokenType.Asterisk, GreetingSyntaxErrorFormat, token);

				token = await ReadTokenAsync (doAsync, cancellationToken).ConfigureAwait (false);

				AssertToken (token, ImapTokenType.Atom, GreetingSyntaxErrorFormat, token);

				var atom = (string) token.Value;
				var text = string.Empty;
				var state = State;
				var bye = false;

				switch (atom.ToUpperInvariant ()) {
				case "BYE":
					bye = true;
					break;
				case "PREAUTH":
					state = ImapEngineState.Authenticated;
					break;
				case "OK":
					state = ImapEngineState.Connected;
					break;
				default:
					throw UnexpectedToken (GreetingSyntaxErrorFormat, token);
				}

				token = await ReadTokenAsync (doAsync, cancellationToken).ConfigureAwait (false);

				if (token.Type == ImapTokenType.OpenBracket) {
					var code = await ParseResponseCodeAsync (false, doAsync, cancellationToken).ConfigureAwait (false);
					if (code.Type == ImapResponseCodeType.Alert) {
						OnAlert (code.Message);

						if (bye)
							throw new ImapProtocolException (code.Message);
					} else {
						text = code.Message;
					}
				} else if (token.Type != ImapTokenType.Eoln) {
					text = (string) token.Value;
					text += await ReadLineAsync (doAsync, cancellationToken).ConfigureAwait (false);
					text = text.TrimEnd ();

					if (bye)
						throw new ImapProtocolException (text);
				} else if (bye) {
					throw new ImapProtocolException ("The IMAP server unexpectedly refused the connection.");
				}

				if (text.StartsWith ("Courier-IMAP ready.", StringComparison.Ordinal))
					QuirksMode = ImapQuirksMode.Courier;
				else if (text.Contains (" Cyrus IMAP "))
					QuirksMode = ImapQuirksMode.Cyrus;
				else if (text.StartsWith ("Domino IMAP4 Server", StringComparison.Ordinal))
					QuirksMode = ImapQuirksMode.Domino;
				else if (text.StartsWith ("Dovecot ready.", StringComparison.Ordinal))
					QuirksMode = ImapQuirksMode.Dovecot;
				else if (text.StartsWith ("Microsoft Exchange Server 2007 IMAP4 service is ready", StringComparison.Ordinal))
					QuirksMode = ImapQuirksMode.Exchange;
				else if (text.StartsWith ("The Microsoft Exchange IMAP4 service is ready.", StringComparison.Ordinal))
					QuirksMode = ImapQuirksMode.Exchange;
				else if (text.StartsWith ("Gimap ready", StringComparison.Ordinal))
					QuirksMode = ImapQuirksMode.GMail;
				else if (text.Contains (" IMAP4rev1 2007f.") || text.Contains (" Panda IMAP "))
					QuirksMode = ImapQuirksMode.UW;
				else if (text.Contains ("SmarterMail"))
					QuirksMode = ImapQuirksMode.SmarterMail;
				else if (text.Contains ("Yandex IMAP4rev1 "))
					QuirksMode = ImapQuirksMode.Yandex;

				State = state;
			} catch {
				Disconnect ();
				throw;
			}
		}

		/// <summary>
		/// Disconnects the <see cref="ImapEngine"/>.
		/// </summary>
		/// <remarks>
		/// Disconnects the <see cref="ImapEngine"/>.
		/// </remarks>
		public void Disconnect ()
		{
			if (Selected != null) {
				Selected.Reset ();
				Selected.OnClosed ();
				Selected = null;
			}

			current = null;

			if (Stream != null) {
				Stream.Dispose ();
				Stream = null;
			}

			if (State != ImapEngineState.Disconnected) {
				State = ImapEngineState.Disconnected;
				OnDisconnected ();
			}
		}

		internal async Task<string> ReadLineAsync (bool doAsync, CancellationToken cancellationToken)
		{
			using (var memory = new MemoryStream ()) {
				bool complete;
				byte[] buf;
				int count;

				do {
					if (doAsync)
						complete = await Stream.ReadLineAsync (memory, cancellationToken).ConfigureAwait (false);
					else
						complete = Stream.ReadLine (memory, cancellationToken);
				} while (!complete);

				count = (int) memory.Length;
#if !NETSTANDARD1_3 && !NETSTANDARD1_6
				buf = memory.GetBuffer ();
#else
				buf = memory.ToArray ();
#endif

				try {
					return UTF8.GetString (buf, 0, count);
				} catch (DecoderFallbackException) {
					return Latin1.GetString (buf, 0, count);
				}
			}
		}

#if false
		/// <summary>
		/// Reads a single line from the <see cref="ImapStream"/>.
		/// </summary>
		/// <returns>The line.</returns>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.InvalidOperationException">
		/// The engine is not connected.
		/// </exception>
		/// <exception cref="System.OperationCanceledException">
		/// The operation was canceled via the cancellation token.
		/// </exception>
		/// <exception cref="System.IO.IOException">
		/// An I/O error occurred.
		/// </exception>
		/// <exception cref="ImapProtocolException">
		/// An IMAP protocol error occurred.
		/// </exception>
		public string ReadLine (CancellationToken cancellationToken)
		{
			return ReadLineAsync (false, cancellationToken).GetAwaiter ().GetResult ();
		}

		/// <summary>
		/// Asynchronously reads a single line from the <see cref="ImapStream"/>.
		/// </summary>
		/// <returns>The line.</returns>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.InvalidOperationException">
		/// The engine is not connected.
		/// </exception>
		/// <exception cref="System.OperationCanceledException">
		/// The operation was canceled via the cancellation token.
		/// </exception>
		/// <exception cref="System.IO.IOException">
		/// An I/O error occurred.
		/// </exception>
		/// <exception cref="ImapProtocolException">
		/// An IMAP protocol error occurred.
		/// </exception>
		public Task<string> ReadLineAsync (CancellationToken cancellationToken)
		{
			return ReadLineAsync (true, cancellationToken);
		}
#endif

		internal Task<ImapToken> ReadTokenAsync (string specials, bool doAsync, CancellationToken cancellationToken)
		{
			return Stream.ReadTokenAsync (specials, doAsync, cancellationToken);
		}

		internal Task<ImapToken> ReadTokenAsync (bool doAsync, CancellationToken cancellationToken)
		{
			return Stream.ReadTokenAsync (ImapStream.DefaultSpecials, doAsync, cancellationToken);
		}

		internal async Task<ImapToken> PeekTokenAsync (string specials, bool doAsync, CancellationToken cancellationToken)
		{
			var token = await ReadTokenAsync (specials, doAsync, cancellationToken).ConfigureAwait (false);

			Stream.UngetToken (token);

			return token;
		}

		internal Task<ImapToken> PeekTokenAsync (bool doAsync, CancellationToken cancellationToken)
		{
			return PeekTokenAsync (ImapStream.DefaultSpecials, doAsync, cancellationToken);
		}

		/// <summary>
		/// Reads the next token.
		/// </summary>
		/// <returns>The token.</returns>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.InvalidOperationException">
		/// The engine is not connected.
		/// </exception>
		/// <exception cref="System.OperationCanceledException">
		/// The operation was canceled via the cancellation token.
		/// </exception>
		/// <exception cref="System.IO.IOException">
		/// An I/O error occurred.
		/// </exception>
		/// <exception cref="ImapProtocolException">
		/// An IMAP protocol error occurred.
		/// </exception>
		public ImapToken ReadToken (CancellationToken cancellationToken)
		{
			return Stream.ReadToken (cancellationToken);
		}

#if false
		/// <summary>
		/// Asynchronously reads the next token.
		/// </summary>
		/// <returns>The token.</returns>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.InvalidOperationException">
		/// The engine is not connected.
		/// </exception>
		/// <exception cref="System.OperationCanceledException">
		/// The operation was canceled via the cancellation token.
		/// </exception>
		/// <exception cref="System.IO.IOException">
		/// An I/O error occurred.
		/// </exception>
		/// <exception cref="ImapProtocolException">
		/// An IMAP protocol error occurred.
		/// </exception>
		public Task<ImapToken> ReadTokenAsync (CancellationToken cancellationToken)
		{
			return Stream.ReadTokenAsync (cancellationToken);
		}

		/// <summary>
		/// Peeks at the next token.
		/// </summary>
		/// <returns>The next token.</returns>
		/// <param name="specials">A list of characters that are not legal in bare string tokens.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.InvalidOperationException">
		/// The engine is not connected.
		/// </exception>
		/// <exception cref="System.OperationCanceledException">
		/// The operation was canceled via the cancellation token.
		/// </exception>
		/// <exception cref="System.IO.IOException">
		/// An I/O error occurred.
		/// </exception>
		/// <exception cref="ImapProtocolException">
		/// An IMAP protocol error occurred.
		/// </exception>
		public ImapToken PeekToken (string specials, CancellationToken cancellationToken)
		{
			return PeekTokenAsync (specials, false, cancellationToken).GetAwaiter ().GetResult ();
		}

		/// <summary>
		/// Asynchronously peeks at the next token.
		/// </summary>
		/// <returns>The next token.</returns>
		/// <param name="specials">A list of characters that are not legal in bare string tokens.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.InvalidOperationException">
		/// The engine is not connected.
		/// </exception>
		/// <exception cref="System.OperationCanceledException">
		/// The operation was canceled via the cancellation token.
		/// </exception>
		/// <exception cref="System.IO.IOException">
		/// An I/O error occurred.
		/// </exception>
		/// <exception cref="ImapProtocolException">
		/// An IMAP protocol error occurred.
		/// </exception>
		public Task<ImapToken> PeekTokenAsync (string specials, CancellationToken cancellationToken)
		{
			return PeekTokenAsync (specials, true, cancellationToken);
		}

		/// <summary>
		/// Peeks at the next token.
		/// </summary>
		/// <returns>The next token.</returns>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.InvalidOperationException">
		/// The engine is not connected.
		/// </exception>
		/// <exception cref="System.OperationCanceledException">
		/// The operation was canceled via the cancellation token.
		/// </exception>
		/// <exception cref="System.IO.IOException">
		/// An I/O error occurred.
		/// </exception>
		/// <exception cref="ImapProtocolException">
		/// An IMAP protocol error occurred.
		/// </exception>
		public ImapToken PeekToken (CancellationToken cancellationToken)
		{
			return PeekTokenAsync (false, cancellationToken).GetAwaiter ().GetResult ();
		}

		/// <summary>
		/// Asynchronously peeks at the next token.
		/// </summary>
		/// <returns>The next token.</returns>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.InvalidOperationException">
		/// The engine is not connected.
		/// </exception>
		/// <exception cref="System.OperationCanceledException">
		/// The operation was canceled via the cancellation token.
		/// </exception>
		/// <exception cref="System.IO.IOException">
		/// An I/O error occurred.
		/// </exception>
		/// <exception cref="ImapProtocolException">
		/// An IMAP protocol error occurred.
		/// </exception>
		public Task<ImapToken> PeekTokenAsync (CancellationToken cancellationToken)
		{
			return PeekTokenAsync (true, cancellationToken);
		}
#endif

		internal async Task<string> ReadLiteralAsync (bool doAsync, CancellationToken cancellationToken)
		{
			if (Stream.Mode != ImapStreamMode.Literal)
				throw new InvalidOperationException ();

			using (var memory = new MemoryStream (Stream.LiteralLength)) {
				var buf = ArrayPool<byte>.Shared.Rent (BufferSize);
				int nread;

				try {
					if (doAsync) {
						while ((nread = await Stream.ReadAsync (buf, 0, BufferSize, cancellationToken).ConfigureAwait (false)) > 0)
							memory.Write (buf, 0, nread);
					} else {
						while ((nread = Stream.Read (buf, 0, BufferSize, cancellationToken)) > 0)
							memory.Write (buf, 0, nread);
					}
				} finally {
					ArrayPool<byte>.Shared.Return (buf);
				}

				nread = (int) memory.Length;
#if !NETSTANDARD1_3 && !NETSTANDARD1_6
				buf = memory.GetBuffer ();
#else
				buf = memory.ToArray ();
#endif

				return Latin1.GetString (buf, 0, nread);
			}
		}

#if false
		/// <summary>
		/// Reads the literal as a string.
		/// </summary>
		/// <returns>The literal.</returns>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.InvalidOperationException">
		/// The <see cref="Stream"/> is not in literal mode.
		/// </exception>
		/// <exception cref="System.OperationCanceledException">
		/// The operation was canceled via the cancellation token.
		/// </exception>
		/// <exception cref="System.IO.IOException">
		/// An I/O error occurred.
		/// </exception>
		public string ReadLiteral (CancellationToken cancellationToken)
		{
			return ReadLiteralAsync (false, cancellationToken).GetAwaiter ().GetResult ();
		}

		/// <summary>
		/// Asynchronously reads the literal as a string.
		/// </summary>
		/// <returns>The literal.</returns>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.InvalidOperationException">
		/// The <see cref="Stream"/> is not in literal mode.
		/// </exception>
		/// <exception cref="System.OperationCanceledException">
		/// The operation was canceled via the cancellation token.
		/// </exception>
		/// <exception cref="System.IO.IOException">
		/// An I/O error occurred.
		/// </exception>
		public Task<string> ReadLiteralAsync (CancellationToken cancellationToken)
		{
			return ReadLiteralAsync (true, cancellationToken);
		}
#endif

		async Task SkipLineAsync (bool doAsync, CancellationToken cancellationToken)
		{
			ImapToken token;

			do {
				token = await ReadTokenAsync (doAsync, cancellationToken).ConfigureAwait (false);

				if (token.Type == ImapTokenType.Literal) {
					var buf = ArrayPool<byte>.Shared.Rent (BufferSize);
					int nread;

					try {
						do {
							if (doAsync)
								nread = await Stream.ReadAsync (buf, 0, BufferSize, cancellationToken).ConfigureAwait (false);
							else
								nread = Stream.Read (buf, 0, BufferSize, cancellationToken);
						} while (nread > 0);
					} finally {
						ArrayPool<byte>.Shared.Return (buf);
					}
				}
			} while (token.Type != ImapTokenType.Eoln);
		}

		async Task UpdateCapabilitiesAsync (ImapTokenType sentinel, bool doAsync, CancellationToken cancellationToken)
		{
			// Clear the extensions except STARTTLS so that this capability stays set after a STARTTLS command.
			ProtocolVersion = ImapProtocolVersion.Unknown;
			Capabilities &= ImapCapabilities.StartTLS;
			AuthenticationMechanisms.Clear ();
			CompressionAlgorithms.Clear ();
			ThreadingAlgorithms.Clear ();
			SupportedContexts.Clear ();
			CapabilitiesVersion++;
			AppendLimit = null;
			Rights.Clear ();
			I18NLevel = 0;

			var token = await ReadTokenAsync (doAsync, cancellationToken).ConfigureAwait (false);

			while (token.Type == ImapTokenType.Atom) {
				var atom = (string) token.Value;

				if (atom.StartsWith ("AUTH=", StringComparison.OrdinalIgnoreCase)) {
					AuthenticationMechanisms.Add (atom.Substring ("AUTH=".Length));
				} else if (atom.StartsWith ("APPENDLIMIT=", StringComparison.OrdinalIgnoreCase)) {
					uint limit;

					if (uint.TryParse (atom.Substring ("APPENDLIMIT=".Length), NumberStyles.None, CultureInfo.InvariantCulture, out limit))
						AppendLimit = limit;

					Capabilities |= ImapCapabilities.AppendLimit;
				} else if (atom.StartsWith ("COMPRESS=", StringComparison.OrdinalIgnoreCase)) {
					CompressionAlgorithms.Add (atom.Substring ("COMPRESS=".Length));
					Capabilities |= ImapCapabilities.Compress;
				} else if (atom.StartsWith ("CONTEXT=", StringComparison.OrdinalIgnoreCase)) {
					SupportedContexts.Add (atom.Substring ("CONTEXT=".Length));
					Capabilities |= ImapCapabilities.Context;
				} else if (atom.StartsWith ("I18NLEVEL=", StringComparison.OrdinalIgnoreCase)) {
					int level;

					int.TryParse (atom.Substring ("I18NLEVEL=".Length), NumberStyles.None, CultureInfo.InvariantCulture, out level);
					I18NLevel = level;

					Capabilities |= ImapCapabilities.I18NLevel;
				} else if (atom.StartsWith ("RIGHTS=", StringComparison.OrdinalIgnoreCase)) {
					var rights = atom.Substring ("RIGHTS=".Length);
					Rights.AddRange (rights);
				} else if (atom.StartsWith ("THREAD=", StringComparison.OrdinalIgnoreCase)) {
					var algorithm = atom.Substring ("THREAD=".Length);
					switch (algorithm.ToUpperInvariant ()) {
					case "ORDEREDSUBJECT":
						ThreadingAlgorithms.Add (ThreadingAlgorithm.OrderedSubject);
						break;
					case "REFERENCES":
						ThreadingAlgorithms.Add (ThreadingAlgorithm.References);
						break;
					}

					Capabilities |= ImapCapabilities.Thread;
				} else {
					switch (atom.ToUpperInvariant ()) {
					case "IMAP4":                 Capabilities |= ImapCapabilities.IMAP4; break;
					case "IMAP4REV1":             Capabilities |= ImapCapabilities.IMAP4rev1; break;
					case "STATUS":                Capabilities |= ImapCapabilities.Status; break;
					case "ACL":                   Capabilities |= ImapCapabilities.Acl; break;
					case "QUOTA":                 Capabilities |= ImapCapabilities.Quota; break;
					case "LITERAL+":              Capabilities |= ImapCapabilities.LiteralPlus; break;
					case "IDLE":                  Capabilities |= ImapCapabilities.Idle; break;
					case "MAILBOX-REFERRALS":     Capabilities |= ImapCapabilities.MailboxReferrals; break;
					case "LOGIN-REFERRALS":       Capabilities |= ImapCapabilities.LoginReferrals; break;
					case "NAMESPACE":             Capabilities |= ImapCapabilities.Namespace; break;
					case "ID":                    Capabilities |= ImapCapabilities.Id; break;
					case "CHILDREN":              Capabilities |= ImapCapabilities.Children; break;
					case "LOGINDISABLED":         Capabilities |= ImapCapabilities.LoginDisabled; break;
					case "STARTTLS":              Capabilities |= ImapCapabilities.StartTLS; break;
					case "MULTIAPPEND":           Capabilities |= ImapCapabilities.MultiAppend; break;
					case "BINARY":                Capabilities |= ImapCapabilities.Binary; break;
					case "UNSELECT":              Capabilities |= ImapCapabilities.Unselect; break;
					case "UIDPLUS":               Capabilities |= ImapCapabilities.UidPlus; break;
					case "CATENATE":              Capabilities |= ImapCapabilities.Catenate; break;
					case "CONDSTORE":             Capabilities |= ImapCapabilities.CondStore; break;
					case "ESEARCH":               Capabilities |= ImapCapabilities.ESearch; break;
					case "SASL-IR":               Capabilities |= ImapCapabilities.SaslIR; break;
					case "WITHIN":                Capabilities |= ImapCapabilities.Within; break;
					case "ENABLE":                Capabilities |= ImapCapabilities.Enable; break;
					case "QRESYNC":               Capabilities |= ImapCapabilities.QuickResync; break;
					case "SEARCHRES":             Capabilities |= ImapCapabilities.SearchResults; break;
					case "SORT":                  Capabilities |= ImapCapabilities.Sort; break;
					case "ANNOTATE-EXPERIMENT-1": Capabilities |= ImapCapabilities.Annotate; break;
					case "LIST-EXTENDED":         Capabilities |= ImapCapabilities.ListExtended; break;
					case "CONVERT":               Capabilities |= ImapCapabilities.Convert; break;
					case "LANGUAGE":              Capabilities |= ImapCapabilities.Language; break;
					case "ESORT":                 Capabilities |= ImapCapabilities.ESort; break;
					case "METADATA":              Capabilities |= ImapCapabilities.Metadata; break;
					case "METADATA-SERVER":       Capabilities |= ImapCapabilities.MetadataServer; break;
					case "NOTIFY":                Capabilities |= ImapCapabilities.Notify; break;
					case "LIST-STATUS":           Capabilities |= ImapCapabilities.ListStatus; break;
					case "SORT=DISPLAY":          Capabilities |= ImapCapabilities.SortDisplay; break;
					case "CREATE-SPECIAL-USE":    Capabilities |= ImapCapabilities.CreateSpecialUse; break;
					case "SPECIAL-USE":           Capabilities |= ImapCapabilities.SpecialUse; break;
					case "SEARCH=FUZZY":          Capabilities |= ImapCapabilities.FuzzySearch; break;
					case "MULTISEARCH":           Capabilities |= ImapCapabilities.MultiSearch; break;
					case "MOVE":                  Capabilities |= ImapCapabilities.Move; break;
					case "UTF8=ACCEPT":           Capabilities |= ImapCapabilities.UTF8Accept; break;
					case "UTF8=ONLY":             Capabilities |= ImapCapabilities.UTF8Only; break;
					case "LITERAL-":              Capabilities |= ImapCapabilities.LiteralMinus; break;
					case "APPENDLIMIT":           Capabilities |= ImapCapabilities.AppendLimit; break;
					case "UNAUTHENTICATE":        Capabilities |= ImapCapabilities.Unauthenticate; break;
					case "STATUS=SIZE":           Capabilities |= ImapCapabilities.StatusSize; break;
					case "LIST-MYRIGHTS":         Capabilities |= ImapCapabilities.ListMyRights; break;
					case "OBJECTID":              Capabilities |= ImapCapabilities.ObjectID; break;
					case "REPLACE":               Capabilities |= ImapCapabilities.Replace; break;
					case "SAVEDATE":              Capabilities |= ImapCapabilities.SaveDate; break;
					case "XLIST":                 Capabilities |= ImapCapabilities.XList; break;
					case "X-GM-EXT-1":            Capabilities |= ImapCapabilities.GMailExt1; QuirksMode = ImapQuirksMode.GMail; break;
					case "XSTOP":                 QuirksMode = ImapQuirksMode.ProtonMail; break;
					case "X-SUN-IMAP":            QuirksMode = ImapQuirksMode.SunMicrosystems; break;
					case "XYMHIGHESTMODSEQ":      QuirksMode = ImapQuirksMode.Yahoo; break;
					}
				}

				token = await ReadTokenAsync (doAsync, cancellationToken).ConfigureAwait (false);
			}

			AssertToken (token, sentinel, GenericItemSyntaxErrorFormat, "CAPABILITIES", token);

			// unget the sentinel
			Stream.UngetToken (token);

			if ((Capabilities & ImapCapabilities.IMAP4rev1) != 0) {
				ProtocolVersion = ImapProtocolVersion.IMAP4rev1;
				Capabilities |= ImapCapabilities.Status;
			} else if ((Capabilities & ImapCapabilities.IMAP4) != 0) {
				ProtocolVersion = ImapProtocolVersion.IMAP4;
			}

			if ((Capabilities & ImapCapabilities.QuickResync) != 0)
				Capabilities |= ImapCapabilities.CondStore;

			if ((Capabilities & ImapCapabilities.UTF8Only) != 0)
				Capabilities |= ImapCapabilities.UTF8Accept;
		}

		async Task UpdateNamespacesAsync (bool doAsync, CancellationToken cancellationToken)
		{
			var namespaces = new List<FolderNamespaceCollection> {
				PersonalNamespaces, OtherNamespaces, SharedNamespaces
			};
			ImapFolder folder;
			ImapToken token;
			string path;
			char delim;
			int n = 0;

			PersonalNamespaces.Clear ();
			SharedNamespaces.Clear ();
			OtherNamespaces.Clear ();

			token = await ReadTokenAsync (doAsync, cancellationToken).ConfigureAwait (false);

			do {
				if (token.Type == ImapTokenType.OpenParen) {
					// parse the list of namespace pairs...
					token = await ReadTokenAsync (doAsync, cancellationToken).ConfigureAwait (false);

					while (token.Type == ImapTokenType.OpenParen) {
						// parse the namespace pair - first token is the path
						token = await ReadTokenAsync (doAsync, cancellationToken).ConfigureAwait (false);

						AssertToken (token, ImapTokenType.Atom, ImapTokenType.QString, GenericUntaggedResponseSyntaxErrorFormat, "NAMESPACE", token);

						path = (string) token.Value;

						// second token is the directory separator
						token = await ReadTokenAsync (doAsync, cancellationToken).ConfigureAwait (false);

						AssertToken (token, ImapTokenType.QString, ImapTokenType.Nil, GenericUntaggedResponseSyntaxErrorFormat, "NAMESPACE", token);

						var qstring = token.Type == ImapTokenType.Nil ? string.Empty : (string) token.Value;

						if (qstring.Length > 0) {
							delim = qstring[0];

							// canonicalize the namespace path
							path = path.TrimEnd (delim);
						} else {
							delim = '\0';
						}

						namespaces[n].Add (new FolderNamespace (delim, DecodeMailboxName (path)));

						if (!GetCachedFolder (path, out folder)) {
							folder = CreateImapFolder (path, FolderAttributes.None, delim);
							CacheFolder (folder);
						}

						folder.UpdateIsNamespace (true);

						do {
							token = await ReadTokenAsync (doAsync, cancellationToken).ConfigureAwait (false);

							if (token.Type == ImapTokenType.CloseParen)
								break;

							// NAMESPACE extension

							AssertToken (token, ImapTokenType.Atom, ImapTokenType.QString, GenericUntaggedResponseSyntaxErrorFormat, "NAMESPACE", token);

							token = await ReadTokenAsync (doAsync, cancellationToken).ConfigureAwait (false);

							AssertToken (token, ImapTokenType.OpenParen, GenericUntaggedResponseSyntaxErrorFormat, "NAMESPACE", token);

							do {
								token = await ReadTokenAsync (doAsync, cancellationToken).ConfigureAwait (false);

								if (token.Type == ImapTokenType.CloseParen)
									break;

								AssertToken (token, ImapTokenType.Atom, ImapTokenType.QString, GenericUntaggedResponseSyntaxErrorFormat, "NAMESPACE", token);
							} while (true);
						} while (true);

						// read the next token - it should either be '(' or ')'
						token = await ReadTokenAsync (doAsync, cancellationToken).ConfigureAwait (false);
					}

					AssertToken (token, ImapTokenType.CloseParen, GenericUntaggedResponseSyntaxErrorFormat, "NAMESPACE", token);
				} else {
					AssertToken (token, ImapTokenType.Nil, GenericUntaggedResponseSyntaxErrorFormat, "NAMESPACE", token);
				}

				token = await ReadTokenAsync (doAsync, cancellationToken).ConfigureAwait (false);
				n++;
			} while (n < 3);

			while (token.Type != ImapTokenType.Eoln)
				token = await ReadTokenAsync (doAsync, cancellationToken).ConfigureAwait (false);
		}

		void ProcessResponseCodes (ImapCommand ic)
		{
			foreach (var code in ic.RespCodes) {
				switch (code.Type) {
				case ImapResponseCodeType.Alert:
					OnAlert (code.Message);
					break;
				case ImapResponseCodeType.NotificationOverflow:
					OnNotificationOverflow ();
					break;
				}
			}
		}

		void EmitMetadataChanged (Metadata metadata)
		{
			var encodedName = metadata.EncodedName;
			ImapFolder folder;

			if (encodedName.Length == 0) {
				OnMetadataChanged (metadata);
			} else if (FolderCache.TryGetValue (encodedName, out folder)) {
				folder.OnMetadataChanged (metadata);
			}
		}

		internal MetadataCollection FilterMetadata (MetadataCollection metadata, string encodedName)
		{
			for (int i = 0; i < metadata.Count; i++) {
				if (metadata[i].EncodedName == encodedName)
					continue;

				EmitMetadataChanged (metadata[i]);
				metadata.RemoveAt (i);
				i--;
			}

			return metadata;
		}

		internal void ProcessMetadataChanges (MetadataCollection metadata)
		{
			for (int i = 0; i < metadata.Count; i++)
				EmitMetadataChanged (metadata[i]);
		}

		internal static ImapResponseCodeType GetResponseCodeType (string atom)
		{
			switch (atom.ToUpperInvariant ()) {
			case "ALERT":                return ImapResponseCodeType.Alert;
			case "BADCHARSET":           return ImapResponseCodeType.BadCharset;
			case "CAPABILITY":           return ImapResponseCodeType.Capability;
			case "NEWNAME":              return ImapResponseCodeType.NewName;
			case "PARSE":                return ImapResponseCodeType.Parse;
			case "PERMANENTFLAGS":       return ImapResponseCodeType.PermanentFlags;
			case "READ-ONLY":            return ImapResponseCodeType.ReadOnly;
			case "READ-WRITE":           return ImapResponseCodeType.ReadWrite;
			case "TRYCREATE":            return ImapResponseCodeType.TryCreate;
			case "UIDNEXT":              return ImapResponseCodeType.UidNext;
			case "UIDVALIDITY":          return ImapResponseCodeType.UidValidity;
			case "UNSEEN":               return ImapResponseCodeType.Unseen;
			case "REFERRAL":             return ImapResponseCodeType.Referral;
			case "UNKNOWN-CTE":          return ImapResponseCodeType.UnknownCte;
			case "APPENDUID":            return ImapResponseCodeType.AppendUid;
			case "COPYUID":              return ImapResponseCodeType.CopyUid;
			case "UIDNOTSTICKY":         return ImapResponseCodeType.UidNotSticky;
			case "URLMECH":              return ImapResponseCodeType.UrlMech;
			case "BADURL":               return ImapResponseCodeType.BadUrl;
			case "TOOBIG":               return ImapResponseCodeType.TooBig;
			case "HIGHESTMODSEQ":        return ImapResponseCodeType.HighestModSeq;
			case "MODIFIED":             return ImapResponseCodeType.Modified;
			case "NOMODSEQ":             return ImapResponseCodeType.NoModSeq;
			case "COMPRESSIONACTIVE":    return ImapResponseCodeType.CompressionActive;
			case "CLOSED":               return ImapResponseCodeType.Closed;
			case "NOTSAVED":             return ImapResponseCodeType.NotSaved;
			case "BADCOMPARATOR":        return ImapResponseCodeType.BadComparator;
			case "ANNOTATE":             return ImapResponseCodeType.Annotate;
			case "ANNOTATIONS":          return ImapResponseCodeType.Annotations;
			case "MAXCONVERTMESSAGES":   return ImapResponseCodeType.MaxConvertMessages;
			case "MAXCONVERTPARTS":      return ImapResponseCodeType.MaxConvertParts;
			case "TEMPFAIL":             return ImapResponseCodeType.TempFail;
			case "NOUPDATE":             return ImapResponseCodeType.NoUpdate;
			case "METADATA":             return ImapResponseCodeType.Metadata;
			case "NOTIFICATIONOVERFLOW": return ImapResponseCodeType.NotificationOverflow;
			case "BADEVENT":             return ImapResponseCodeType.BadEvent;
			case "UNDEFINED-FILTER":     return ImapResponseCodeType.UndefinedFilter;
			case "UNAVAILABLE":          return ImapResponseCodeType.Unavailable;
			case "AUTHENTICATIONFAILED": return ImapResponseCodeType.AuthenticationFailed;
			case "AUTHORIZATIONFAILED":  return ImapResponseCodeType.AuthorizationFailed;
			case "EXPIRED":              return ImapResponseCodeType.Expired;
			case "PRIVACYREQUIRED":      return ImapResponseCodeType.PrivacyRequired;
			case "CONTACTADMIN":         return ImapResponseCodeType.ContactAdmin;
			case "NOPERM":               return ImapResponseCodeType.NoPerm;
			case "INUSE":                return ImapResponseCodeType.InUse;
			case "EXPUNGEISSUED":        return ImapResponseCodeType.ExpungeIssued;
			case "CORRUPTION":           return ImapResponseCodeType.Corruption;
			case "SERVERBUG":            return ImapResponseCodeType.ServerBug;
			case "CLIENTBUG":            return ImapResponseCodeType.ClientBug;
			case "CANNOT":               return ImapResponseCodeType.CanNot;
			case "LIMIT":                return ImapResponseCodeType.Limit;
			case "OVERQUOTA":            return ImapResponseCodeType.OverQuota;
			case "ALREADYEXISTS":        return ImapResponseCodeType.AlreadyExists;
			case "NONEXISTENT":          return ImapResponseCodeType.NonExistent;
			case "USEATTR":              return ImapResponseCodeType.UseAttr;
			case "MAILBOXID":            return ImapResponseCodeType.MailboxId;
			default:                     return ImapResponseCodeType.Unknown;
			}
		}

		/// <summary>
		/// Parses the response code.
		/// </summary>
		/// <returns>The response code.</returns>
		/// <param name="isTagged">Whether or not the resp-code is tagged vs untagged.</param>
		/// <param name="doAsync">Whether or not asynchronous IO methods should be used.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		public async Task<ImapResponseCode> ParseResponseCodeAsync (bool isTagged, bool doAsync, CancellationToken cancellationToken)
		{
			uint validity = Selected != null ? Selected.UidValidity : 0;
			ImapResponseCode code;
			ImapToken token;
			string atom;

//			token = await ReadTokenAsync (doAsync, cancellationToken).ConfigureAwait (false);
//
//			if (token.Type != ImapTokenType.LeftBracket) {
//				Debug.WriteLine ("Expected a '[' followed by a RESP-CODE, but got: {0}", token);
//				throw UnexpectedToken (token, false);
//			}

			token = await ReadTokenAsync (doAsync, cancellationToken).ConfigureAwait (false);

			AssertToken (token, ImapTokenType.Atom, "Syntax error in response code. {0}", token);

			atom = (string) token.Value;
			token = await ReadTokenAsync (doAsync, cancellationToken).ConfigureAwait (false);

			code = ImapResponseCode.Create (GetResponseCodeType (atom));
			code.IsTagged = isTagged;

			switch (code.Type) {
			case ImapResponseCodeType.BadCharset:
				if (token.Type == ImapTokenType.OpenParen) {
					token = await ReadTokenAsync (doAsync, cancellationToken).ConfigureAwait (false);

					SupportedCharsets.Clear ();
					while (token.Type == ImapTokenType.Atom || token.Type == ImapTokenType.QString) {
						SupportedCharsets.Add ((string) token.Value);
						token = await ReadTokenAsync (doAsync, cancellationToken).ConfigureAwait (false);
					}

					AssertToken (token, ImapTokenType.CloseParen, GenericResponseCodeSyntaxErrorFormat, "BADCHARSET", token);

					token = await ReadTokenAsync (doAsync, cancellationToken).ConfigureAwait (false);
				}
				break;
			case ImapResponseCodeType.Capability:
				Stream.UngetToken (token);
				await UpdateCapabilitiesAsync (ImapTokenType.CloseBracket, doAsync, cancellationToken).ConfigureAwait (false);
				token = await ReadTokenAsync (doAsync, cancellationToken).ConfigureAwait (false);
				break;
			case ImapResponseCodeType.PermanentFlags:
				var perm = (PermanentFlagsResponseCode) code;

				Stream.UngetToken (token);
				perm.Flags = await ImapUtils.ParseFlagsListAsync (this, "PERMANENTFLAGS", null, doAsync, cancellationToken).ConfigureAwait (false);
				token = await ReadTokenAsync (doAsync, cancellationToken).ConfigureAwait (false);
				break;
			case ImapResponseCodeType.UidNext:
				var next = (UidNextResponseCode) code;

				// Note: we allow '0' here because some servers have been known to send "* OK [UIDNEXT 0]".
				// The *probable* explanation here is that the folder has never been opened and/or no messages
				// have ever been delivered (yet) to that mailbox and so the UIDNEXT has not (yet) been
				// initialized.
				//
				// See https://github.com/jstedfast/MailKit/issues/1010 for an example.
				var uid = ParseNumber (token, false, GenericResponseCodeSyntaxErrorFormat, "UIDNEXT", token);
				next.Uid = uid > 0 ? new UniqueId (uid) : UniqueId.Invalid;
				token = await ReadTokenAsync (doAsync, cancellationToken).ConfigureAwait (false);
				break;
			case ImapResponseCodeType.UidValidity:
				var uidvalidity = (UidValidityResponseCode) code;

				// Note: we allow '0' here because some servers have been known to send "* OK [UIDVALIDITY 0]".
				// The *probable* explanation here is that the folder has never been opened and/or no messages
				// have ever been delivered (yet) to that mailbox and so the UIDVALIDITY has not (yet) been
				// initialized.
				//
				// See https://github.com/jstedfast/MailKit/issues/150 for an example.
				uidvalidity.UidValidity = ParseNumber (token, false, GenericResponseCodeSyntaxErrorFormat, "UIDVALIDITY", token);
				token = await ReadTokenAsync (doAsync, cancellationToken).ConfigureAwait (false);
				break;
			case ImapResponseCodeType.Unseen:
				var unseen = (UnseenResponseCode) code;

				// Note: we allow '0' here because some servers have been known to send "* OK [UNSEEN 0]" when the
				// mailbox contains no messages.
				//
				// See https://github.com/jstedfast/MailKit/issues/34 for details.
				var n = ParseNumber (token, false, GenericResponseCodeSyntaxErrorFormat, "UNSEEN", token);

				unseen.Index = n > 0 ? (int) (n - 1) : 0;

				token = await ReadTokenAsync (doAsync, cancellationToken).ConfigureAwait (false);
				break;
			case ImapResponseCodeType.NewName:
				var rename = (NewNameResponseCode) code;

				// Note: this RESP-CODE existed in rfc2060 but has been removed in rfc3501:
				//
				// 85) Remove NEWNAME.  It can't work because mailbox names can be
				// literals and can include "]".  Functionality can be addressed via
				// referrals.
				AssertToken (token, ImapTokenType.Atom, ImapTokenType.QString, GenericResponseCodeSyntaxErrorFormat, "NEWNAME", token);

				rename.OldName = (string) token.Value;

				// the next token should be another atom or qstring token representing the new name of the folder
				token = await ReadTokenAsync (doAsync, cancellationToken).ConfigureAwait (false);

				AssertToken (token, ImapTokenType.Atom, ImapTokenType.QString, GenericResponseCodeSyntaxErrorFormat, "NEWNAME", token);

				rename.NewName = (string) token.Value;

				token = await ReadTokenAsync (doAsync, cancellationToken).ConfigureAwait (false);
				break;
			case ImapResponseCodeType.AppendUid:
				var append = (AppendUidResponseCode) code;

				append.UidValidity = ParseNumber (token, false, GenericResponseCodeSyntaxErrorFormat, "APPENDUID", token);

				token = await ReadTokenAsync (doAsync, cancellationToken).ConfigureAwait (false);

				// The MULTIAPPEND extension redefines APPENDUID's second argument to be a uid-set instead of a single uid.
				append.UidSet = ParseUidSet (token, append.UidValidity, GenericResponseCodeSyntaxErrorFormat, "APPENDUID", token);

				token = await ReadTokenAsync (doAsync, cancellationToken).ConfigureAwait (false);
				break;
			case ImapResponseCodeType.CopyUid:
				var copy = (CopyUidResponseCode) code;

				copy.UidValidity = ParseNumber (token, false, GenericResponseCodeSyntaxErrorFormat, "COPYUID", token);

				token = await ReadTokenAsync (doAsync, cancellationToken).ConfigureAwait (false);

				// Note: Outlook.com will apparently sometimes issue a [COPYUID nz_number SPACE SPACE] resp-code
				// in response to a UID COPY or UID MOVE command. Likely this happens only when the source message
				// didn't exist or something? See https://github.com/jstedfast/MailKit/issues/555 for details.

				if (token.Type != ImapTokenType.CloseBracket) {
					copy.SrcUidSet = ParseUidSet (token, validity, GenericResponseCodeSyntaxErrorFormat, "COPYUID", token);
				} else {
					copy.SrcUidSet = new UniqueIdSet ();
					Stream.UngetToken (token);
				}

				token = await ReadTokenAsync (doAsync, cancellationToken).ConfigureAwait (false);

				if (token.Type != ImapTokenType.CloseBracket) {
					copy.DestUidSet = ParseUidSet (token, copy.UidValidity, GenericResponseCodeSyntaxErrorFormat, "COPYUID", token);
				} else {
					copy.DestUidSet = new UniqueIdSet ();
					Stream.UngetToken (token);
				}

				token = await ReadTokenAsync (doAsync, cancellationToken).ConfigureAwait (false);
				break;
			case ImapResponseCodeType.BadUrl:
				var badurl = (BadUrlResponseCode) code;

				AssertToken (token, ImapTokenType.Atom, ImapTokenType.QString, GenericResponseCodeSyntaxErrorFormat, "BADURL", token);

				badurl.BadUrl = (string) token.Value;

				token = await ReadTokenAsync (doAsync, cancellationToken).ConfigureAwait (false);
				break;
			case ImapResponseCodeType.HighestModSeq:
				var highest = (HighestModSeqResponseCode) code;

				highest.HighestModSeq = ParseNumber64 (token, false, GenericResponseCodeSyntaxErrorFormat, "HIGHESTMODSEQ", token);

				token = await ReadTokenAsync (doAsync, cancellationToken).ConfigureAwait (false);
				break;
			case ImapResponseCodeType.Modified:
				var modified = (ModifiedResponseCode) code;

				modified.UidSet = ParseUidSet (token, validity, GenericResponseCodeSyntaxErrorFormat, "MODIFIED", token);

				token = await ReadTokenAsync (doAsync, cancellationToken).ConfigureAwait (false);
				break;
			case ImapResponseCodeType.MaxConvertMessages:
			case ImapResponseCodeType.MaxConvertParts:
				var maxConvert = (MaxConvertResponseCode) code;

				maxConvert.MaxConvert = ParseNumber (token, false, GenericResponseCodeSyntaxErrorFormat, atom, token);

				token = await ReadTokenAsync (doAsync, cancellationToken).ConfigureAwait (false);
				break;
			case ImapResponseCodeType.NoUpdate:
				var noUpdate = (NoUpdateResponseCode) code;

				AssertToken (token, ImapTokenType.Atom, ImapTokenType.QString, GenericResponseCodeSyntaxErrorFormat, "NOUPDATE", token);

				noUpdate.Tag = (string) token.Value;

				token = await ReadTokenAsync (doAsync, cancellationToken).ConfigureAwait (false);
				break;
			case ImapResponseCodeType.Annotate:
				var annotate = (AnnotateResponseCode) code;

				AssertToken (token, ImapTokenType.Atom, GenericResponseCodeSyntaxErrorFormat, "ANNOTATE", token);

				switch (((string) token.Value).ToUpperInvariant ()) {
				case "TOOBIG":
					annotate.SubType = AnnotateResponseCodeSubType.TooBig;
					break;
				case "TOOMANY":
					annotate.SubType = AnnotateResponseCodeSubType.TooMany;
					break;
				}

				token = await ReadTokenAsync (doAsync, cancellationToken).ConfigureAwait (false);
				break;
			case ImapResponseCodeType.Annotations:
				var annotations = (AnnotationsResponseCode) code;

				AssertToken (token, ImapTokenType.Atom, GenericResponseCodeSyntaxErrorFormat, "ANNOTATIONS", token);

				switch (((string) token.Value).ToUpperInvariant ()) {
				case "NONE": break;
				case "READ-ONLY":
					annotations.Access = AnnotationAccess.ReadOnly;
					break;
				default:
					annotations.Access = AnnotationAccess.ReadWrite;
					annotations.MaxSize = ParseNumber (token, false, GenericResponseCodeSyntaxErrorFormat, "ANNOTATIONS", token);
					break;
				}

				token = await ReadTokenAsync (doAsync, cancellationToken).ConfigureAwait (false);

				if (annotations.Access != AnnotationAccess.None) {
					annotations.Scopes = AnnotationScope.Both;

					if (token.Type != ImapTokenType.CloseBracket) {
						AssertToken (token, ImapTokenType.Atom, GenericResponseCodeSyntaxErrorFormat, "ANNOTATIONS", token);

						if (((string) token.Value).Equals ("NOPRIVATE", StringComparison.OrdinalIgnoreCase))
							annotations.Scopes = AnnotationScope.Shared;

						token = await ReadTokenAsync (doAsync, cancellationToken).ConfigureAwait (false);
					}
				}

				break;
			case ImapResponseCodeType.Metadata:
				var metadata = (MetadataResponseCode) code;

				AssertToken (token, ImapTokenType.Atom, GenericResponseCodeSyntaxErrorFormat, "METADATA", token);

				switch (((string) token.Value).ToUpperInvariant ()) {
				case "LONGENTRIES":
					metadata.SubType = MetadataResponseCodeSubType.LongEntries;
					metadata.IsError = false;

					token = await ReadTokenAsync (doAsync, cancellationToken).ConfigureAwait (false);

					metadata.Value = ParseNumber (token, false, GenericResponseCodeSyntaxErrorFormat, "METADATA LONGENTRIES", token);
					break;
				case "MAXSIZE":
					metadata.SubType = MetadataResponseCodeSubType.MaxSize;

					token = await ReadTokenAsync (doAsync, cancellationToken).ConfigureAwait (false);

					metadata.Value = ParseNumber (token, false, GenericResponseCodeSyntaxErrorFormat, "METADATA MAXSIZE", token);
					break;
				case "TOOMANY":
					metadata.SubType = MetadataResponseCodeSubType.TooMany;
					break;
				case "NOPRIVATE":
					metadata.SubType = MetadataResponseCodeSubType.NoPrivate;
					break;
				}

				token = await ReadTokenAsync (doAsync, cancellationToken).ConfigureAwait (false);
				break;
			case ImapResponseCodeType.UndefinedFilter:
				var undefined = (UndefinedFilterResponseCode) code;

				AssertToken (token, ImapTokenType.Atom, GenericResponseCodeSyntaxErrorFormat, "UNDEFINED-FILTER", token);

				undefined.Name = (string) token.Value;

				token = await ReadTokenAsync (doAsync, cancellationToken).ConfigureAwait (false);
				break;
			case ImapResponseCodeType.MailboxId:
				var mailboxid = (MailboxIdResponseCode) code;

				AssertToken (token, ImapTokenType.OpenParen, GenericResponseCodeSyntaxErrorFormat, "MAILBOXID", token);

				token = await ReadTokenAsync (doAsync, cancellationToken).ConfigureAwait (false);

				AssertToken (token, ImapTokenType.Atom, GenericResponseCodeSyntaxErrorFormat, "MAILBOXID", token);

				mailboxid.MailboxId = (string) token.Value;

				token = await ReadTokenAsync (doAsync, cancellationToken).ConfigureAwait (false);

				AssertToken (token, ImapTokenType.CloseParen, GenericResponseCodeSyntaxErrorFormat, "MAILBOXID", token);

				token = await ReadTokenAsync (doAsync, cancellationToken).ConfigureAwait (false);
				break;
			default:
				// Note: This code-path handles: [ALERT], [CLOSED], [READ-ONLY], [READ-WRITE], etc.

				//if (code.Type == ImapResponseCodeType.Unknown)
				//	Debug.WriteLine (string.Format ("Unknown RESP-CODE encountered: {0}", atom));

				// extensions are of the form: "[" atom [SPACE 1*<any TEXT_CHAR except "]">] "]"

				// skip over tokens until we get to a ']'
				while (token.Type != ImapTokenType.CloseBracket && token.Type != ImapTokenType.Eoln)
					token = await ReadTokenAsync (doAsync, cancellationToken).ConfigureAwait (false);

				break;
			}

			AssertToken (token, ImapTokenType.CloseBracket, "Syntax error in response code. {0}", token);

			code.Message = (await ReadLineAsync (doAsync, cancellationToken).ConfigureAwait (false)).Trim ();

			return code;
		}

		async Task UpdateStatusAsync (bool doAsync, CancellationToken cancellationToken)
		{
			var token = await ReadTokenAsync (ImapStream.AtomSpecials, doAsync, cancellationToken).ConfigureAwait (false);
			ImapFolder folder;
			uint count, uid;
			ulong modseq;
			string name;

			switch (token.Type) {
			case ImapTokenType.Literal:
				name = await ReadLiteralAsync (doAsync, cancellationToken).ConfigureAwait (false);
				break;
			case ImapTokenType.QString:
			case ImapTokenType.Atom:
				name = (string) token.Value;
				break;
			case ImapTokenType.Nil:
				// Note: according to rfc3501, section 4.5, NIL is acceptable as a mailbox name.
				name = "NIL";
				break;
			default:
				throw UnexpectedToken (GenericUntaggedResponseSyntaxErrorFormat, "STATUS", token);
			}

			// Note: if the folder is null, then it probably means the user is using NOTIFY
			// and hasn't yet requested the folder. That's ok.
			GetCachedFolder (name, out folder);

			token = await ReadTokenAsync (doAsync, cancellationToken).ConfigureAwait (false);

			AssertToken (token, ImapTokenType.OpenParen, GenericUntaggedResponseSyntaxErrorFormat, "STATUS", token);

			do {
				token = await ReadTokenAsync (doAsync, cancellationToken).ConfigureAwait (false);

				if (token.Type == ImapTokenType.CloseParen)
					break;

				AssertToken (token, ImapTokenType.Atom, GenericUntaggedResponseSyntaxErrorFormat, "STATUS", token);

				var atom = (string) token.Value;

				token = await ReadTokenAsync (doAsync, cancellationToken).ConfigureAwait (false);

				switch (atom.ToUpperInvariant ()) {
				case "HIGHESTMODSEQ":
					AssertToken (token, ImapTokenType.Atom, GenericUntaggedResponseSyntaxErrorFormat, "STATUS", token);

					modseq = ParseNumber64 (token, false, GenericItemSyntaxErrorFormat, atom, token);

					if (folder != null)
						folder.UpdateHighestModSeq (modseq);
					break;
				case "MESSAGES":
					AssertToken (token, ImapTokenType.Atom, GenericUntaggedResponseSyntaxErrorFormat, "STATUS", token);

					count = ParseNumber (token, false, GenericItemSyntaxErrorFormat, atom, token);

					if (folder != null)
						folder.OnExists ((int) count);
					break;
				case "RECENT":
					AssertToken (token, ImapTokenType.Atom, GenericUntaggedResponseSyntaxErrorFormat, "STATUS", token);

					count = ParseNumber (token, false, GenericItemSyntaxErrorFormat, atom, token);

					if (folder != null)
						folder.OnRecent ((int) count);
					break;
				case "UIDNEXT":
					AssertToken (token, ImapTokenType.Atom, GenericUntaggedResponseSyntaxErrorFormat, "STATUS", token);

					uid = ParseNumber (token, false, GenericItemSyntaxErrorFormat, atom, token);

					if (folder != null)
						folder.UpdateUidNext (uid > 0 ? new UniqueId (uid) : UniqueId.Invalid);
					break;
				case "UIDVALIDITY":
					AssertToken (token, ImapTokenType.Atom, GenericUntaggedResponseSyntaxErrorFormat, "STATUS", token);

					uid = ParseNumber (token, false, GenericItemSyntaxErrorFormat, atom, token);

					if (folder != null)
						folder.UpdateUidValidity (uid);
					break;
				case "UNSEEN":
					AssertToken (token, ImapTokenType.Atom, GenericUntaggedResponseSyntaxErrorFormat, "STATUS", token);

					count = ParseNumber (token, false, GenericItemSyntaxErrorFormat, atom, token);

					if (folder != null)
						folder.UpdateUnread ((int) count);
					break;
				case "APPENDLIMIT":
					if (token.Type == ImapTokenType.Atom) {
						var limit = ParseNumber (token, false, GenericItemSyntaxErrorFormat, atom, token);

						if (folder != null)
							folder.UpdateAppendLimit (limit);
					} else {
						AssertToken (token, ImapTokenType.Nil, GenericUntaggedResponseSyntaxErrorFormat, "STATUS", token);

						if (folder != null)
							folder.UpdateAppendLimit (null);
					}
					break;
				case "SIZE":
					AssertToken (token, ImapTokenType.Atom, GenericUntaggedResponseSyntaxErrorFormat, "STATUS", token);

					var size = ParseNumber64 (token, false, GenericItemSyntaxErrorFormat, atom, token);

					if (folder != null)
						folder.UpdateSize (size);
					break;
				case "MAILBOXID":
					AssertToken (token, ImapTokenType.OpenParen, GenericUntaggedResponseSyntaxErrorFormat, "STATUS", token);

					token = await ReadTokenAsync (doAsync, cancellationToken).ConfigureAwait (false);

					AssertToken (token, ImapTokenType.Atom, GenericItemSyntaxErrorFormat, atom, token);

					if (folder != null)
						folder.UpdateId ((string) token.Value);

					token = await ReadTokenAsync (doAsync, cancellationToken).ConfigureAwait (false);

					AssertToken (token, ImapTokenType.CloseParen, GenericUntaggedResponseSyntaxErrorFormat, "STATUS", token);
					break;
				}
			} while (true);

			token = await ReadTokenAsync (doAsync, cancellationToken).ConfigureAwait (false);

			AssertToken (token, ImapTokenType.Eoln, GenericUntaggedResponseSyntaxErrorFormat, "STATUS", token);
		}

		/// <summary>
		/// Processes an untagged response.
		/// </summary>
		/// <returns>The untagged response.</returns>
		/// <param name="doAsync">Whether or not asynchronous IO methods should be used.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		internal async Task<ImapUntaggedResult> ProcessUntaggedResponseAsync (bool doAsync, CancellationToken cancellationToken)
		{
			var token = await ReadTokenAsync (doAsync, cancellationToken).ConfigureAwait (false);
			var folder = current.Folder ?? Selected;
			var result = ImapUntaggedResult.Handled;
			ImapUntaggedHandler handler;
			uint number;
			string atom;

			// Note: work around broken IMAP servers such as home.pl which sends "* [COPYUID ...]" resp-codes
			// See https://github.com/jstedfast/MailKit/issues/115#issuecomment-313684616 for details.
			if (token.Type == ImapTokenType.OpenBracket) {
				// unget the '[' token and then pretend that we got an "OK"
				Stream.UngetToken (token);
				atom = "OK";
			} else if (token.Type != ImapTokenType.Atom) {
				// if we get anything else here, just ignore it?
				Stream.UngetToken (token);
				await SkipLineAsync (doAsync, cancellationToken).ConfigureAwait (false);
				return result;
			} else {
				atom = (string) token.Value;
			}

			switch (atom.ToUpperInvariant ()) {
			case "BYE":
				token = await ReadTokenAsync (doAsync, cancellationToken).ConfigureAwait (false);

				if (token.Type == ImapTokenType.OpenBracket) {
					var code = await ParseResponseCodeAsync (false, doAsync, cancellationToken).ConfigureAwait (false);
					current.RespCodes.Add (code);
				} else {
					var text = token.Value.ToString () + await ReadLineAsync (doAsync, cancellationToken).ConfigureAwait (false);
					current.ResponseText = text.TrimEnd ();
				}

				current.Bye = true;

				// Note: Yandex IMAP is broken and will continue sending untagged BYE responses until the client closes
				// the connection. In order to avoid this scenario, consider this command complete as soon as we receive
				// the very first untagged BYE response and do not hold out hoping for a tagged response following the
				// untagged BYE.
				//
				// See https://github.com/jstedfast/MailKit/issues/938 for details.
				if (QuirksMode == ImapQuirksMode.Yandex && !current.Logout)
					current.Status = ImapCommandStatus.Complete;
				break;
			case "CAPABILITY":
				await UpdateCapabilitiesAsync (ImapTokenType.Eoln, doAsync, cancellationToken);

				// read the eoln token
				await ReadTokenAsync (doAsync, cancellationToken).ConfigureAwait (false);
				break;
			case "ENABLED":
				do {
					token = await ReadTokenAsync (doAsync, cancellationToken).ConfigureAwait (false);

					if (token.Type == ImapTokenType.Eoln)
						break;

					AssertToken (token, ImapTokenType.Atom, GenericUntaggedResponseSyntaxErrorFormat, atom, token);

					var feature = (string) token.Value;
					switch (feature.ToUpperInvariant ()) {
					case "UTF8=ACCEPT": UTF8Enabled = true; break;
					case "QRESYNC": QResyncEnabled = true; break;
					}
				} while (true);
				break;
			case "FLAGS":
				folder.UpdateAcceptedFlags (await ImapUtils.ParseFlagsListAsync (this, atom, null, doAsync, cancellationToken).ConfigureAwait (false));
				token = await ReadTokenAsync (doAsync, cancellationToken).ConfigureAwait (false);

				AssertToken (token, ImapTokenType.Eoln, GenericUntaggedResponseSyntaxErrorFormat, atom, token);
				break;
			case "NAMESPACE":
				await UpdateNamespacesAsync (doAsync, cancellationToken).ConfigureAwait (false);
				break;
			case "STATUS":
				await UpdateStatusAsync (doAsync, cancellationToken).ConfigureAwait (false);
				break;
			case "OK": case "NO": case "BAD":
				if (atom.Equals ("OK", StringComparison.OrdinalIgnoreCase))
					result = ImapUntaggedResult.Ok;
				else if (atom.Equals ("NO", StringComparison.OrdinalIgnoreCase))
					result = ImapUntaggedResult.No;
				else
					result = ImapUntaggedResult.Bad;

				token = await ReadTokenAsync (doAsync, cancellationToken).ConfigureAwait (false);

				if (token.Type == ImapTokenType.OpenBracket) {
					var code = await ParseResponseCodeAsync (false, doAsync, cancellationToken).ConfigureAwait (false);
					current.RespCodes.Add (code);
				} else if (token.Type != ImapTokenType.Eoln) {
					var text = ((string) token.Value) + await ReadLineAsync (doAsync, cancellationToken).ConfigureAwait (false);
					current.ResponseText = text.TrimEnd ();
				}
				break;
			default:
				if (uint.TryParse (atom, NumberStyles.None, CultureInfo.InvariantCulture, out number)) {
					// we probably have something like "* 1 EXISTS"
					token = await ReadTokenAsync (doAsync, cancellationToken).ConfigureAwait (false);

					AssertToken (token, ImapTokenType.Atom, "Syntax error in untagged response. {0}", token);

					atom = (string) token.Value;

					if (current.UntaggedHandlers.TryGetValue (atom, out handler)) {
						// the command registered an untagged handler for this atom...
						await handler (this, current, (int) number - 1, doAsync).ConfigureAwait (false);
					} else if (folder != null) {
						switch (atom.ToUpperInvariant ()) {
						case "EXISTS":
							folder.OnExists ((int) number);
							break;
						case "EXPUNGE":
							if (number == 0)
								throw UnexpectedToken ("Syntax error in untagged EXPUNGE response. Unexpected message index: 0");

							folder.OnExpunge ((int) number - 1);
							break;
						case "FETCH":
							// Apparently Courier-IMAP (2004) will reply with "* 0 FETCH ..." sometimes.
							// See https://github.com/jstedfast/MailKit/issues/428 for details.
							//if (number == 0)
							//	throw UnexpectedToken ("Syntax error in untagged FETCH response. Unexpected message index: 0");

							await folder.OnFetchAsync (this, (int) number - 1, doAsync, cancellationToken).ConfigureAwait (false);
							break;
						case "RECENT":
							folder.OnRecent ((int) number);
							break;
						default:
							//Debug.WriteLine ("Unhandled untagged response: * {0} {1}", number, atom);
							break;
						}
					} else {
						//Debug.WriteLine ("Unhandled untagged response: * {0} {1}", number, atom);
					}

					await SkipLineAsync (doAsync, cancellationToken).ConfigureAwait (false);
				} else if (current.UntaggedHandlers.TryGetValue (atom, out handler)) {
					// the command registered an untagged handler for this atom...
					await handler (this, current, -1, doAsync).ConfigureAwait (false);
					await SkipLineAsync (doAsync, cancellationToken).ConfigureAwait (false);
				} else if (atom.Equals ("LIST", StringComparison.OrdinalIgnoreCase)) {
					// unsolicited LIST response - probably due to NOTIFY MailboxName or MailboxSubscribe event
					await ImapUtils.ParseFolderListAsync (this, null, false, true, doAsync, cancellationToken).ConfigureAwait (false);
					token = await ReadTokenAsync (doAsync, cancellationToken).ConfigureAwait (false);
					AssertToken (token, ImapTokenType.Eoln, "Syntax error in untagged LIST response. {0}", token);
				} else if (atom.Equals ("METADATA", StringComparison.OrdinalIgnoreCase)) {
					// unsolicited METADATA response - probably due to NOTIFY MailboxMetadataChange or ServerMetadataChange
					var metadata = new MetadataCollection ();
					await ImapUtils.ParseMetadataAsync (this, metadata, doAsync, cancellationToken).ConfigureAwait (false);
					ProcessMetadataChanges (metadata);

					token = await ReadTokenAsync (doAsync, cancellationToken).ConfigureAwait (false);
					AssertToken (token, ImapTokenType.Eoln, "Syntax error in untagged LIST response. {0}", token);
				} else if (atom.Equals ("VANISHED", StringComparison.OrdinalIgnoreCase) && folder != null) {
					await folder.OnVanishedAsync (this, doAsync, cancellationToken).ConfigureAwait (false);
					await SkipLineAsync (doAsync, cancellationToken).ConfigureAwait (false);
				} else {
					// don't know how to handle this... eat it?
					await SkipLineAsync (doAsync, cancellationToken).ConfigureAwait (false);
				}
				break;
			}

			return result;
		}

		/// <summary>
		/// Iterate the command pipeline.
		/// </summary>
		async Task IterateAsync (bool doAsync)
		{
			lock (queue) {
				if (queue.Count == 0)
					throw new InvalidOperationException ("The IMAP command queue is empty.");

				if (IsBusy)
					throw new InvalidOperationException ("The ImapClient is currently busy processing a command in another thread. Lock the SyncRoot property to properly synchronize your threads.");

				current = queue[0];
				queue.RemoveAt (0);

				try {
					current.CancellationToken.ThrowIfCancellationRequested ();
				} catch {
					queue.RemoveAll (x => x.CancellationToken.IsCancellationRequested);
					current = null;
					throw;
				}
			}

			current.Status = ImapCommandStatus.Active;

			try {
				while (await current.StepAsync (doAsync).ConfigureAwait (false)) {
					// more literal data to send...
				}

				if (current.Bye && !current.Logout)
					throw new ImapProtocolException ("Bye.");
			} catch (ImapProtocolException) {
				var ic = current;

				Disconnect ();

				if (ic.Bye) {
					if (ic.RespCodes.Count > 0) {
						var code = ic.RespCodes[ic.RespCodes.Count - 1];

						if (code.Type == ImapResponseCodeType.Alert) {
							OnAlert (code.Message);

							throw new ImapProtocolException (code.Message);
						}
					}

					if (!string.IsNullOrEmpty (ic.ResponseText))
						throw new ImapProtocolException (ic.ResponseText);
				}

				throw;
			} catch {
				Disconnect ();
				throw;
			} finally {
				current = null;
			}
		}

		/// <summary>
		/// Wait for the specified command to finish.
		/// </summary>
		/// <param name="ic">The IMAP command.</param>
		/// <param name="doAsync">Whether or not asynchronous IO methods should be used.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <paramref name="ic"/> is <c>null</c>.
		/// </exception>
		public async Task RunAsync (ImapCommand ic, bool doAsync)
		{
			if (ic == null)
				throw new ArgumentNullException (nameof (ic));

			while (ic.Status < ImapCommandStatus.Complete) {
				// continue processing commands...
				await IterateAsync (doAsync).ConfigureAwait (false);
			}

			ProcessResponseCodes (ic);
		}

		public IEnumerable<ImapCommand> CreateCommands (CancellationToken cancellationToken, ImapFolder folder, string format, IList<UniqueId> uids, params object[] args)
		{
			var vargs = new List<object> ();
			int maxLength;

			// we assume that uids is the first formatter (with a %s)
			vargs.Add ("1");

			for (int i = 0; i < args.Length; i++)
				vargs.Add (args[i]);

			args = vargs.ToArray ();

			if (QuirksMode == ImapQuirksMode.Courier) {
				// Courier IMAP's command parser allows each token to be up to 16k in size.
				maxLength = 16 * 1024;
			} else {
				int estimated = ImapCommand.EstimateCommandLength (this, format, args);

				switch (QuirksMode) {
				case ImapQuirksMode.Dovecot:
					// Dovecot, by default, allows commands up to 64k.
					// See https://github.com/dovecot/core/blob/master/src/imap/imap-settings.c#L94
					maxLength = Math.Max ((64 * 1042) - estimated, 24);
					break;
				case ImapQuirksMode.GMail:
					// GMail seems to support command-lines up to at least 16k.
					maxLength = Math.Max ((16 * 1042) - estimated, 24);
					break;
				case ImapQuirksMode.Yahoo:
				case ImapQuirksMode.UW:
					// Follow the IMAP4 Implementation Recommendations which states that clients
					// *SHOULD* limit their command lengths to 1000 octets.
					maxLength = Math.Max (1000 - estimated, 24);
					break;
				default:
					// Push the boundaries of the IMAP4 Implementation Recommendations which states
					// that servers *SHOULD* accept command lengths of up to 8000 octets.
					maxLength = Math.Max (8000 - estimated, 24);
					break;
				}
			}

			foreach (var subset in UniqueIdSet.EnumerateSerializedSubsets (uids, maxLength)) {
				args[0] = subset;

				yield return new ImapCommand (this, cancellationToken, folder, format, args);
			}
		}

		public IEnumerable<ImapCommand> QueueCommands (CancellationToken cancellationToken, ImapFolder folder, string format, IList<UniqueId> uids, params object[] args)
		{
			foreach (var ic in CreateCommands (cancellationToken, folder, format, uids, args)) {
				QueueCommand (ic);
				yield return ic;
			}
		}

		/// <summary>
		/// Queues the command.
		/// </summary>
		/// <returns>The command.</returns>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <param name="folder">The folder that the command operates on.</param>
		/// <param name="options">The formatting options.</param>
		/// <param name="format">The command format.</param>
		/// <param name="args">The command arguments.</param>
		public ImapCommand QueueCommand (CancellationToken cancellationToken, ImapFolder folder, FormatOptions options, string format, params object[] args)
		{
			var ic = new ImapCommand (this, cancellationToken, folder, options, format, args);
			QueueCommand (ic);
			return ic;
		}

		/// <summary>
		/// Queues the command.
		/// </summary>
		/// <returns>The command.</returns>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <param name="folder">The folder that the command operates on.</param>
		/// <param name="format">The command format.</param>
		/// <param name="args">The command arguments.</param>
		public ImapCommand QueueCommand (CancellationToken cancellationToken, ImapFolder folder, string format, params object[] args)
		{
			return QueueCommand (cancellationToken, folder, FormatOptions.Default, format, args);
		}

		/// <summary>
		/// Queues the command.
		/// </summary>
		/// <param name="ic">The IMAP command.</param>
		public void QueueCommand (ImapCommand ic)
		{
			lock (queue) {
				ic.Status = ImapCommandStatus.Queued;
				queue.Add (ic);
			}
		}

		/// <summary>
		/// Queries the capabilities.
		/// </summary>
		/// <returns>The command result.</returns>
		/// <param name="doAsync">Whether or not asynchronous IO methods should be used.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		public async Task<ImapCommandResponse> QueryCapabilitiesAsync (bool doAsync, CancellationToken cancellationToken)
		{
			var ic = QueueCommand (cancellationToken, null, "CAPABILITY\r\n");

			await RunAsync (ic, doAsync).ConfigureAwait (false);

			return ic.Response;
		}

		/// <summary>
		/// Cache the specified folder.
		/// </summary>
		/// <param name="folder">The folder.</param>
		public void CacheFolder (ImapFolder folder)
		{
			if ((folder.Attributes & FolderAttributes.Inbox) != 0)
				cacheComparer.DirectorySeparator = folder.DirectorySeparator;

			FolderCache.Add (folder.EncodedName, folder);
		}

		/// <summary>
		/// Gets the cached folder.
		/// </summary>
		/// <returns><c>true</c> if the folder was retreived from the cache; otherwise, <c>false</c>.</returns>
		/// <param name="encodedName">The encoded folder name.</param>
		/// <param name="folder">The cached folder.</param>
		public bool GetCachedFolder (string encodedName, out ImapFolder folder)
		{
			return FolderCache.TryGetValue (encodedName, out folder);
		}

		/// <summary>
		/// Looks up and sets the <see cref="MailFolder.ParentFolder"/> property of each of the folders.
		/// </summary>
		/// <param name="folders">The IMAP folders.</param>
		/// <param name="doAsync">Whether or not asynchronous IO methods should be used.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		internal async Task LookupParentFoldersAsync (IEnumerable<ImapFolder> folders, bool doAsync, CancellationToken cancellationToken)
		{
			var list = new List<ImapFolder> (folders);
			string encodedName, pattern;
			ImapFolder parent;
			int index;

			// Note: we use a for-loop instead of foreach because we conditionally add items to the list.
			for (int i = 0; i < list.Count; i++) {
				var folder = list[i];

				if (folder.ParentFolder != null)
					continue;

				// FIXME: should this search EncodedName instead of FullName?
				if ((index = folder.FullName.LastIndexOf (folder.DirectorySeparator)) != -1) {
					if (index == 0)
						continue;

					var parentName = folder.FullName.Substring (0, index);
					encodedName = EncodeMailboxName (parentName);
				} else {
					encodedName = string.Empty;
				}

				if (GetCachedFolder (encodedName, out parent)) {
					folder.ParentFolder = parent;
					continue;
				}

				// Note: folder names can contain wildcards (including '*' and '%'), so replace '*' with '%'
				// in order to reduce the list of folders returned by our LIST command.
				pattern = encodedName.Replace ('*', '%');

				var command = new StringBuilder ("LIST \"\" %S");
				var returnsSubscribed = false;

				if ((Capabilities & ImapCapabilities.ListExtended) != 0) {
					// Try to get the \Subscribed and \HasChildren or \HasNoChildren attributes
					command.Append (" RETURN (SUBSCRIBED CHILDREN)");
					returnsSubscribed = true;
				}

				command.Append ("\r\n");

				var ic = new ImapCommand (this, cancellationToken, null, command.ToString (), pattern);
				ic.RegisterUntaggedHandler ("LIST", ImapUtils.ParseFolderListAsync);
				ic.ListReturnsSubscribed = returnsSubscribed;
				ic.UserData = new List<ImapFolder> ();

				QueueCommand (ic);

				await RunAsync (ic, doAsync).ConfigureAwait (false);

				if (!GetCachedFolder (encodedName, out parent)) {
					parent = CreateImapFolder (encodedName, FolderAttributes.NonExistent, folder.DirectorySeparator);
					CacheFolder (parent);
				} else if (parent.ParentFolder == null && !parent.IsNamespace) {
					list.Add (parent);
				}

				folder.ParentFolder = parent;
			}
		}

		/// <summary>
		/// Queries the namespaces.
		/// </summary>
		/// <returns>The command result.</returns>
		/// <param name="doAsync">Whether or not asynchronous IO methods should be used.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		public async Task<ImapCommandResponse> QueryNamespacesAsync (bool doAsync, CancellationToken cancellationToken)
		{
			ImapCommand ic;

			if ((Capabilities & ImapCapabilities.Namespace) != 0) {
				ic = QueueCommand (cancellationToken, null, "NAMESPACE\r\n");
				await RunAsync (ic, doAsync).ConfigureAwait (false);
			} else {
				var list = new List<ImapFolder> ();

				ic = new ImapCommand (this, cancellationToken, null, "LIST \"\" \"\"\r\n");
				ic.RegisterUntaggedHandler ("LIST", ImapUtils.ParseFolderListAsync);
				ic.UserData = list;

				QueueCommand (ic);
				await RunAsync (ic, doAsync).ConfigureAwait (false);

				PersonalNamespaces.Clear ();
				SharedNamespaces.Clear ();
				OtherNamespaces.Clear ();

				if (list.Count > 0) {
					var empty = list.FirstOrDefault (x => x.EncodedName.Length == 0);

					if (empty == null) {
						empty = CreateImapFolder (string.Empty, FolderAttributes.None, list[0].DirectorySeparator);
						CacheFolder (empty);
					}

					PersonalNamespaces.Add (new FolderNamespace (empty.DirectorySeparator, empty.FullName));
					empty.UpdateIsNamespace (true);
				}

				await LookupParentFoldersAsync (list, doAsync, cancellationToken).ConfigureAwait (false);
			}

			return ic.Response;
		}

		internal static ImapFolder GetFolder (List<ImapFolder> folders, string encodedName)
		{
			for (int i = 0; i < folders.Count; i++) {
				if (encodedName.Equals (folders[i].EncodedName, StringComparison.OrdinalIgnoreCase))
					return folders[i];
			}

			return null;
		}

		/// <summary>
		/// Assigns a folder as a special folder.
		/// </summary>
		/// <param name="folder">The special folder.</param>
		public void AssignSpecialFolder (ImapFolder folder)
		{
			if ((folder.Attributes & FolderAttributes.All) != 0)
				All = folder;
			if ((folder.Attributes & FolderAttributes.Archive) != 0)
				Archive = folder;
			if ((folder.Attributes & FolderAttributes.Drafts) != 0)
				Drafts = folder;
			if ((folder.Attributes & FolderAttributes.Flagged) != 0)
				Flagged = folder;
			if ((folder.Attributes & FolderAttributes.Important) != 0)
				Important = folder;
			if ((folder.Attributes & FolderAttributes.Junk) != 0)
				Junk = folder;
			if ((folder.Attributes & FolderAttributes.Sent) != 0)
				Sent = folder;
			if ((folder.Attributes & FolderAttributes.Trash) != 0)
				Trash = folder;
		}

		/// <summary>
		/// Assigns the special folders.
		/// </summary>
		/// <param name="list">The list of folders.</param>
		public void AssignSpecialFolders (IList<ImapFolder> list)
		{
			for (int i = 0; i < list.Count; i++)
				AssignSpecialFolder (list[i]);
		}

		/// <summary>
		/// Queries the special folders.
		/// </summary>
		/// <param name="doAsync">Whether or not asynchronous IO methods should be used.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		public async Task QuerySpecialFoldersAsync (bool doAsync, CancellationToken cancellationToken)
		{
			var command = new StringBuilder ("LIST \"\" \"INBOX\"");
			var list = new List<ImapFolder> ();
			var returnsSubscribed = false;
			ImapFolder folder;
			ImapCommand ic;

			if ((Capabilities & ImapCapabilities.ListExtended) != 0) {
				command.Append (" RETURN (SUBSCRIBED CHILDREN)");
				returnsSubscribed = true;
			}

			command.Append ("\r\n");

			ic = new ImapCommand (this, cancellationToken, null, command.ToString ());
			ic.RegisterUntaggedHandler ("LIST", ImapUtils.ParseFolderListAsync);
			ic.ListReturnsSubscribed = returnsSubscribed;
			ic.UserData = list;

			QueueCommand (ic);

			await RunAsync (ic, doAsync).ConfigureAwait (false);

			GetCachedFolder ("INBOX", out folder);
			Inbox = folder;

			list.Clear ();

			if ((Capabilities & ImapCapabilities.SpecialUse) != 0) {
				// Note: Some IMAP servers like ProtonMail respond to SPECIAL-USE LIST queries with BAD, so fall
				// back to just issuing a standard LIST command and hope we get back some SPECIAL-USE attributes.
				//
				// See https://github.com/jstedfast/MailKit/issues/674 for dertails.
				returnsSubscribed = false;
				command.Clear ();

				command.Append ("LIST ");

				if (QuirksMode != ImapQuirksMode.ProtonMail)
					command.Append ("(SPECIAL-USE) \"\" \"*\"");
				else
					command.Append ("\"\" \"%%\"");

				if ((Capabilities & ImapCapabilities.ListExtended) != 0) {
					command.Append (" RETURN (SUBSCRIBED CHILDREN)");
					returnsSubscribed = true;
				}

				command.Append ("\r\n");

				ic = new ImapCommand (this, cancellationToken, null, command.ToString ());
				ic.RegisterUntaggedHandler ("LIST", ImapUtils.ParseFolderListAsync);
				ic.ListReturnsSubscribed = returnsSubscribed;
				ic.UserData = list;

				QueueCommand (ic);

				await RunAsync (ic, doAsync).ConfigureAwait (false);
				await LookupParentFoldersAsync (list, doAsync, cancellationToken).ConfigureAwait (false);

				AssignSpecialFolders (list);
			} else if ((Capabilities & ImapCapabilities.XList) != 0) {
				ic = new ImapCommand (this, cancellationToken, null, "XLIST \"\" \"*\"\r\n");
				ic.RegisterUntaggedHandler ("XLIST", ImapUtils.ParseFolderListAsync);
				ic.UserData = list;

				QueueCommand (ic);

				await RunAsync (ic, doAsync).ConfigureAwait (false);
				await LookupParentFoldersAsync (list, doAsync, cancellationToken).ConfigureAwait (false);

				AssignSpecialFolders (list);
			}
		}

		/// <summary>
		/// Gets the folder representing the specified quota root.
		/// </summary>
		/// <returns>The folder.</returns>
		/// <param name="quotaRoot">The name of the quota root.</param>
		/// <param name="doAsync">Whether or not asynchronous IO methods should be used.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		public async Task<ImapFolder> GetQuotaRootFolderAsync (string quotaRoot, bool doAsync, CancellationToken cancellationToken)
		{
			ImapFolder folder;

			if (GetCachedFolder (quotaRoot, out folder))
				return folder;

			var command = new StringBuilder ("LIST \"\" %S");
			var list = new List<ImapFolder> ();
			var returnsSubscribed = false;

			if ((Capabilities & ImapCapabilities.ListExtended) != 0) {
				command.Append (" RETURN (SUBSCRIBED CHILDREN)");
				returnsSubscribed = true;
			}

			command.Append ("\r\n");

			var ic = new ImapCommand (this, cancellationToken, null, command.ToString (), quotaRoot);
			ic.RegisterUntaggedHandler ("LIST", ImapUtils.ParseFolderListAsync);
			ic.ListReturnsSubscribed = returnsSubscribed;
			ic.UserData = list;

			QueueCommand (ic);

			await RunAsync (ic, doAsync).ConfigureAwait (false);

			if (ic.Response != ImapCommandResponse.Ok)
				throw ImapCommandException.Create ("LIST", ic);

			if ((folder = GetFolder (list, quotaRoot)) == null) {
				folder = CreateImapFolder (quotaRoot, FolderAttributes.NonExistent, '.');
				CacheFolder (folder);
				return folder;
			}

			await LookupParentFoldersAsync (list, doAsync, cancellationToken).ConfigureAwait (false);

			return folder;
		}

		/// <summary>
		/// Gets the folder for the specified path.
		/// </summary>
		/// <returns>The folder.</returns>
		/// <param name="path">The folder path.</param>
		/// <param name="doAsync">Whether or not asynchronous IO methods should be used.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		public async Task<ImapFolder> GetFolderAsync (string path, bool doAsync, CancellationToken cancellationToken)
		{
			var encodedName = EncodeMailboxName (path);
			ImapFolder folder;

			if (GetCachedFolder (encodedName, out folder))
				return folder;

			var command = new StringBuilder ("LIST \"\" %S");
			var list = new List<ImapFolder> ();
			var returnsSubscribed = false;

			if ((Capabilities & ImapCapabilities.ListExtended) != 0) {
				command.Append (" RETURN (SUBSCRIBED CHILDREN)");
				returnsSubscribed = true;
			}

			command.Append ("\r\n");

			var ic = new ImapCommand (this, cancellationToken, null, command.ToString (), encodedName);
			ic.RegisterUntaggedHandler ("LIST", ImapUtils.ParseFolderListAsync);
			ic.ListReturnsSubscribed = returnsSubscribed;
			ic.UserData = list;

			QueueCommand (ic);

			await RunAsync (ic, doAsync).ConfigureAwait (false);

			if (ic.Response != ImapCommandResponse.Ok)
				throw ImapCommandException.Create ("LIST", ic);

			if ((folder = GetFolder (list, encodedName)) == null)
				throw new FolderNotFoundException (path);

			await LookupParentFoldersAsync (list, doAsync, cancellationToken).ConfigureAwait (false);

			return folder;
		}

		internal string GetStatusQuery (StatusItems items)
		{
			var flags = string.Empty;

			if ((items & StatusItems.Count) != 0)
				flags += "MESSAGES ";
			if ((items & StatusItems.Recent) != 0)
				flags += "RECENT ";
			if ((items & StatusItems.UidNext) != 0)
				flags += "UIDNEXT ";
			if ((items & StatusItems.UidValidity) != 0)
				flags += "UIDVALIDITY ";
			if ((items & StatusItems.Unread) != 0)
				flags += "UNSEEN ";

			if ((Capabilities & ImapCapabilities.CondStore) != 0) {
				if ((items & StatusItems.HighestModSeq) != 0)
					flags += "HIGHESTMODSEQ ";
			}

			// Note: If the IMAP server specifies a limit in the CAPABILITY response, then
			// it seems we cannot expect to be able to query this in a STATUS command...
			if ((Capabilities & ImapCapabilities.AppendLimit) != 0 && !AppendLimit.HasValue) {
				if ((items & StatusItems.AppendLimit) != 0)
					flags += "APPENDLIMIT ";
			}

			if ((Capabilities & ImapCapabilities.StatusSize) != 0) {
				if ((items & StatusItems.Size) != 0)
					flags += "SIZE ";
			}

			if ((Capabilities & ImapCapabilities.ObjectID) != 0) {
				if ((items & StatusItems.MailboxId) != 0)
					flags += "MAILBOXID ";
			}

			return flags.TrimEnd ();
		}

		/// <summary>
		/// Get all of the folders within the specified namespace.
		/// </summary>
		/// <remarks>
		/// Gets all of the folders within the specified namespace.
		/// </remarks>
		/// <returns>The list of folders.</returns>
		/// <param name="namespace">The namespace.</param>
		/// <param name="items">The status items to pre-populate.</param>
		/// <param name="subscribedOnly">If set to <c>true</c>, only subscribed folders will be listed.</param>
		/// <param name="doAsync">Whether or not asynchronous IO methods should be used.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		public async Task<IList<ImapFolder>> GetFoldersAsync (FolderNamespace @namespace, StatusItems items, bool subscribedOnly, bool doAsync, CancellationToken cancellationToken)
		{
			var encodedName = EncodeMailboxName (@namespace.Path);
			var pattern = encodedName.Length > 0 ? encodedName + @namespace.DirectorySeparator : string.Empty;
			var status = items != StatusItems.None;
			var list = new List<ImapFolder> ();
			var command = new StringBuilder ();
			var returnsSubscribed = false;
			var lsub = subscribedOnly;
			ImapFolder folder;

			if (!GetCachedFolder (encodedName, out folder))
				throw new FolderNotFoundException (@namespace.Path);

			if (subscribedOnly) {
				if ((Capabilities & ImapCapabilities.ListExtended) != 0) {
					command.Append ("LIST (SUBSCRIBED)");
					returnsSubscribed = true;
					lsub = false;
				} else {
					command.Append ("LSUB");
				}
			} else {
				command.Append ("LIST");
			}

			command.Append (" \"\" %S");

			if (!lsub) {
				if (items != StatusItems.None && (Capabilities & ImapCapabilities.ListStatus) != 0) {
					command.Append (" RETURN (");

					if ((Capabilities & ImapCapabilities.ListExtended) != 0) {
						if (!subscribedOnly) {
							command.Append ("SUBSCRIBED ");
							returnsSubscribed = true;
						}
						command.Append ("CHILDREN ");
					}

					command.AppendFormat ("STATUS ({0})", GetStatusQuery (items));
					command.Append (')');
					status = false;
				} else if ((Capabilities & ImapCapabilities.ListExtended) != 0) {
					command.Append (" RETURN (");
					if (!subscribedOnly) {
						command.Append ("SUBSCRIBED ");
						returnsSubscribed = true;
					}
					command.Append ("CHILDREN");
					command.Append (')');
				}
			}

			command.Append ("\r\n");

			var ic = new ImapCommand (this, cancellationToken, null, command.ToString (), pattern + "*");
			ic.RegisterUntaggedHandler (lsub ? "LSUB" : "LIST", ImapUtils.ParseFolderListAsync);
			ic.ListReturnsSubscribed = returnsSubscribed;
			ic.UserData = list;
			ic.Lsub = lsub;

			QueueCommand (ic);

			await RunAsync (ic, doAsync).ConfigureAwait (false);

			if (ic.Response != ImapCommandResponse.Ok)
				throw ImapCommandException.Create (lsub ? "LSUB" : "LIST", ic);

			await LookupParentFoldersAsync (list, doAsync, cancellationToken).ConfigureAwait (false);

			if (status) {
				for (int i = 0; i < list.Count; i++) {
					if (list[i].Exists)
						await list[i].StatusAsync (items, doAsync, false, cancellationToken).ConfigureAwait (false);
				}
			}

			return list;
		}

		/// <summary>
		/// Decodes the name of the mailbox.
		/// </summary>
		/// <returns>The mailbox name.</returns>
		/// <param name="encodedName">The encoded name.</param>
		public string DecodeMailboxName (string encodedName)
		{
			return UTF8Enabled ? encodedName : ImapEncoding.Decode (encodedName);
		}

		/// <summary>
		/// Encodes the name of the mailbox.
		/// </summary>
		/// <returns>The mailbox name.</returns>
		/// <param name="mailboxName">The encoded mailbox name.</param>
		public string EncodeMailboxName (string mailboxName)
		{
			return UTF8Enabled ? mailboxName : ImapEncoding.Encode (mailboxName);
		}

		/// <summary>
		/// Determines whether the mailbox name is valid or not.
		/// </summary>
		/// <returns><c>true</c> if the mailbox name is valid; otherwise, <c>false</c>.</returns>
		/// <param name="mailboxName">The mailbox name.</param>
		/// <param name="delim">The path delimeter.</param>
		public bool IsValidMailboxName (string mailboxName, char delim)
		{
			// From rfc6855:
			//
			// Mailbox names MUST comply with the Net-Unicode Definition ([RFC5198], Section 2)
			// with the specific exception that they MUST NOT contain control characters
			// (U+0000-U+001F and U+0080-U+009F), a delete character (U+007F), a line separator (U+2028),
			// or a paragraph separator (U+2029).
			for (int i = 0; i < mailboxName.Length; i++) {
				char c = mailboxName[i];

				if (c <= 0x1F || (c >= 0x80 && c <= 0x9F) || c == 0x7F || c == 0x2028 || c == 0x2029 || c == delim)
					return false;
			}

			return mailboxName.Length > 0;
		}

		void InitializeParser (Stream stream, bool persistent)
		{
			if (parser == null)
				parser = new MimeParser (ParserOptions.Default, stream, persistent);
			else
				parser.SetStream (ParserOptions.Default, stream, persistent);
		}

		public async Task<HeaderList> ParseHeadersAsync (Stream stream, bool doAsync, CancellationToken cancellationToken)
		{
			InitializeParser (stream, false);

			if (doAsync)
				return await parser.ParseHeadersAsync (cancellationToken).ConfigureAwait (false);

			return parser.ParseHeaders (cancellationToken);
		}

		public async Task<MimeMessage> ParseMessageAsync (Stream stream, bool persistent, bool doAsync, CancellationToken cancellationToken)
		{
			InitializeParser (stream, persistent);

			if (doAsync)
				return await parser.ParseMessageAsync (cancellationToken).ConfigureAwait (false);

			return parser.ParseMessage (cancellationToken);
		}

		public async Task<MimeEntity> ParseEntityAsync (Stream stream, bool persistent, bool doAsync, CancellationToken cancellationToken)
		{
			InitializeParser (stream, persistent);

			if (doAsync)
				return await parser.ParseEntityAsync (cancellationToken).ConfigureAwait (false);

			return parser.ParseEntity (cancellationToken);
		}

		/// <summary>
		/// Occurs when the engine receives an alert message from the server.
		/// </summary>
		public event EventHandler<AlertEventArgs> Alert;

		internal void OnAlert (string message)
		{
			var handler = Alert;

			if (handler != null)
				handler (this, new AlertEventArgs (message));
		}

		/// <summary>
		/// Occurs when the engine receives a notification that a folder has been created.
		/// </summary>
		public event EventHandler<FolderCreatedEventArgs> FolderCreated;

		internal void OnFolderCreated (IMailFolder folder)
		{
			var handler = FolderCreated;

			if (handler != null)
				handler (this, new FolderCreatedEventArgs (folder));
		}

		/// <summary>
		/// Occurs when the engine receives a notification that metadata has changed.
		/// </summary>
		public event EventHandler<MetadataChangedEventArgs> MetadataChanged;

		internal void OnMetadataChanged (Metadata metadata)
		{
			var handler = MetadataChanged;

			if (handler != null)
				handler (this, new MetadataChangedEventArgs (metadata));
		}

		/// <summary>
		/// Occurs when the engine receives a notification overflow message from the server.
		/// </summary>
		public event EventHandler<EventArgs> NotificationOverflow;

		internal void OnNotificationOverflow ()
		{
			// [NOTIFICATIONOVERFLOW] will reset to NOTIFY NONE
			NotifySelectedNewExpunge = false;

			var handler = NotificationOverflow;

			if (handler != null)
				handler (this, EventArgs.Empty);
		}

		public event EventHandler<EventArgs> Disconnected;

		void OnDisconnected ()
		{
			var handler = Disconnected;

			if (handler != null)
				handler (this, EventArgs.Empty);
		}

		/// <summary>
		/// Releases all resource used by the <see cref="MailKit.Net.Imap.ImapEngine"/> object.
		/// </summary>
		/// <remarks>Call <see cref="Dispose"/> when you are finished using the <see cref="MailKit.Net.Imap.ImapEngine"/>. The
		/// <see cref="Dispose"/> method leaves the <see cref="MailKit.Net.Imap.ImapEngine"/> in an unusable state. After
		/// calling <see cref="Dispose"/>, you must release all references to the <see cref="MailKit.Net.Imap.ImapEngine"/> so
		/// the garbage collector can reclaim the memory that the <see cref="MailKit.Net.Imap.ImapEngine"/> was occupying.</remarks>
		public void Dispose ()
		{
			disposed = true;
			Disconnect ();
		}
	}
}
