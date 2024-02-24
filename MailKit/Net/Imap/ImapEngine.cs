//
// ImapEngine.cs
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
		IMAP4rev1,
		IMAP4rev2,
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
		Exchange2003,
		Exchange2007,
		GMail,
		hMailServer,
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

		static int TagPrefixIndex;

#if NET6_0_OR_GREATER
		readonly ClientMetrics metrics;
#endif

		internal readonly Dictionary<string, ImapFolder> FolderCache;
		readonly CreateImapFolderDelegate createImapFolder;
		readonly ImapFolderNameComparer cacheComparer;
		internal ImapQuirksMode QuirksMode;
		readonly List<ImapCommand> queue;
		long clientConnectedTimestamp;
		internal char TagPrefix;
		ImapCommand current;
		MimeParser parser;
		internal int Tag;
		bool disposed;

		public ImapEngine (CreateImapFolderDelegate createImapFolderDelegate)
		{
#if NET6_0_OR_GREATER
			// Use the globally configured Pop3Client metrics.
			metrics = Telemetry.ImapClient.Metrics;
#endif

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

			TagPrefix = (char) ('A' + (TagPrefixIndex++ % 26));
		}

		/// <summary>
		/// Get the authentication mechanisms supported by the IMAP server.
		/// </summary>
		/// <remarks>
		/// The authentication mechanisms are queried durring the
		/// <see cref="Connect"/> or <see cref="ConnectAsync"/> methods.
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
		/// <see cref="QueryCapabilities"/> and
		/// <see cref="QueryCapabilitiesAsync"/> methods.
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
		/// <see cref="QueryCapabilities"/> and
		/// <see cref="QueryCapabilitiesAsync"/> methods.
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
		/// The capabilities will not be known until a successful connection has been
		/// made via the <see cref="Connect"/> or <see cref="ConnectAsync"/> method.
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
			AssertToken (token, ImapTokenType.Atom, format, args);

			if (!uint.TryParse ((string) token.Value, NumberStyles.None, CultureInfo.InvariantCulture, out var value) || (nonZero && value == 0))
				throw UnexpectedToken (format, args);

			return value;
		}

		internal static ulong ParseNumber64 (ImapToken token, bool nonZero, string format, params object[] args)
		{
			AssertToken (token, ImapTokenType.Atom, format, args);

			if (!ulong.TryParse ((string) token.Value, NumberStyles.None, CultureInfo.InvariantCulture, out var value) || (nonZero && value == 0))
				throw UnexpectedToken (format, args);

			return value;
		}

		internal static bool TryParseNumber64 (ImapToken token, out ulong value)
		{
			return ulong.TryParse ((string) token.Value, NumberStyles.None, CultureInfo.InvariantCulture, out value);
		}

		internal static UniqueIdSet ParseUidSet (ImapToken token, uint validity, out UniqueId? minValue, out UniqueId? maxValue, string format, params object[] args)
		{
			AssertToken (token, ImapTokenType.Atom, format, args);

			if (!UniqueIdSet.TryParse ((string) token.Value, validity, out var uids, out minValue, out maxValue))
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

		public NetworkOperation StartNetworkOperation (string name)
		{
#if NET6_0_OR_GREATER
			return NetworkOperation.Start (name, Uri, Telemetry.ImapClient.ActivitySource, metrics);
#else
			return NetworkOperation.Start (name, Uri);
#endif
		}

		void Initialize (ImapStream stream)
		{
			clientConnectedTimestamp = Stopwatch.GetTimestamp ();
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
		}

		ImapEngineState ParseConnectedState (ImapToken token, out bool bye)
		{
			var atom = (string) token.Value;

			bye = false;

			if (atom.Equals ("OK", StringComparison.OrdinalIgnoreCase)) {
				return ImapEngineState.Connected;
			} else if (atom.Equals ("BYE", StringComparison.OrdinalIgnoreCase)) {
				bye = true;

				return State;
			} else if (atom.Equals ("PREAUTH", StringComparison.OrdinalIgnoreCase)) {
				return ImapEngineState.Authenticated;
			} else {
				throw UnexpectedToken (GreetingSyntaxErrorFormat, token);
			}
		}

		void DetectQuirksMode (string text)
		{
			if (text.StartsWith ("Courier-IMAP ready.", StringComparison.Ordinal))
				QuirksMode = ImapQuirksMode.Courier;
			else if (text.Contains (" Cyrus IMAP "))
				QuirksMode = ImapQuirksMode.Cyrus;
			else if (text.StartsWith ("Domino IMAP4 Server", StringComparison.Ordinal))
				QuirksMode = ImapQuirksMode.Domino;
			else if (text.StartsWith ("Dovecot ready.", StringComparison.Ordinal))
				QuirksMode = ImapQuirksMode.Dovecot;
			else if (text.StartsWith ("Microsoft Exchange Server 2003 IMAP4rev1", StringComparison.Ordinal))
				QuirksMode = ImapQuirksMode.Exchange2003;
			else if (text.StartsWith ("Microsoft Exchange Server 2007 IMAP4 service is ready", StringComparison.Ordinal))
				QuirksMode = ImapQuirksMode.Exchange2007;
			else if (text.StartsWith ("The Microsoft Exchange IMAP4 service is ready.", StringComparison.Ordinal))
				QuirksMode = ImapQuirksMode.Exchange;
			else if (text.StartsWith ("Gimap ready", StringComparison.Ordinal))
				QuirksMode = ImapQuirksMode.GMail;
			else if (text.StartsWith ("IMAPrev1", StringComparison.Ordinal)) // https://github.com/hmailserver/hmailserver/blob/master/hmailserver/source/Server/IMAP/IMAPConnection.cpp#L127
				QuirksMode = ImapQuirksMode.hMailServer;
			else if (text.Contains (" IMAP4rev1 2007f.") || text.Contains (" Panda IMAP "))
				QuirksMode = ImapQuirksMode.UW;
			else if (text.Contains ("SmarterMail"))
				QuirksMode = ImapQuirksMode.SmarterMail;
			else if (text.Contains ("Yandex IMAP4rev1 "))
				QuirksMode = ImapQuirksMode.Yandex;
		}

		/// <summary>
		/// Takes posession of the <see cref="ImapStream"/> and reads the greeting.
		/// </summary>
		/// <param name="stream">The IMAP stream.</param>
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
		public void Connect (ImapStream stream, CancellationToken cancellationToken)
		{
			Initialize (stream);

			try {
				var token = ReadToken (cancellationToken);

				AssertToken (token, ImapTokenType.Asterisk, GreetingSyntaxErrorFormat, token);

				token = ReadToken (cancellationToken);

				AssertToken (token, ImapTokenType.Atom, GreetingSyntaxErrorFormat, token);

				var state = ParseConnectedState (token, out bool bye);
				var text = string.Empty;

				token = ReadToken (cancellationToken);

				if (token.Type == ImapTokenType.OpenBracket) {
					var code = ParseResponseCode (false, cancellationToken);
					if (code.Type == ImapResponseCodeType.Alert) {
						OnAlert (code.Message);

						if (bye)
							throw new ImapProtocolException (code.Message);
					} else {
						text = code.Message;
					}
				} else if (token.Type != ImapTokenType.Eoln) {
					text = ReadLine (cancellationToken).TrimEnd ();
					text = token.Value.ToString () + text;

					if (bye)
						throw new ImapProtocolException (text);
				} else if (bye) {
					throw new ImapProtocolException ("The IMAP server unexpectedly refused the connection.");
				}

				DetectQuirksMode (text);

				State = state;
			} catch (Exception ex) {
				Disconnect (ex);
				throw;
			}
		}

		/// <summary>
		/// Takes posession of the <see cref="ImapStream"/> and reads the greeting.
		/// </summary>
		/// <param name="stream">The IMAP stream.</param>
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
		public async Task ConnectAsync (ImapStream stream, CancellationToken cancellationToken)
		{
			Initialize (stream);

			try {
				var token = await ReadTokenAsync (cancellationToken).ConfigureAwait (false);

				AssertToken (token, ImapTokenType.Asterisk, GreetingSyntaxErrorFormat, token);

				token = await ReadTokenAsync (cancellationToken).ConfigureAwait (false);

				AssertToken (token, ImapTokenType.Atom, GreetingSyntaxErrorFormat, token);

				var state = ParseConnectedState (token, out bool bye);
				var text = string.Empty;

				token = await ReadTokenAsync (cancellationToken).ConfigureAwait (false);

				if (token.Type == ImapTokenType.OpenBracket) {
					var code = await ParseResponseCodeAsync (false, cancellationToken).ConfigureAwait (false);
					if (code.Type == ImapResponseCodeType.Alert) {
						OnAlert (code.Message);

						if (bye)
							throw new ImapProtocolException (code.Message);
					} else {
						text = code.Message;
					}
				} else if (token.Type != ImapTokenType.Eoln) {
					text = (await ReadLineAsync (cancellationToken).ConfigureAwait (false)).TrimEnd ();
					text = token.Value.ToString () + text;

					if (bye)
						throw new ImapProtocolException (text);
				} else if (bye) {
					throw new ImapProtocolException ("The IMAP server unexpectedly refused the connection.");
				}

				DetectQuirksMode (text);

				State = state;
			} catch (Exception ex) {
				Disconnect (ex);
				throw;
			}
		}

		void RecordClientDisconnected (Exception ex)
		{
#if NET6_0_OR_GREATER
			metrics?.RecordClientDisconnected (clientConnectedTimestamp, Uri, ex);
#endif
			clientConnectedTimestamp = 0;
		}

		/// <summary>
		/// Disconnects the <see cref="ImapEngine"/>.
		/// </summary>
		/// <remarks>
		/// Disconnects the <see cref="ImapEngine"/>.
		/// </remarks>
		/// <param name="ex">The exception that is causing the disconnection.</param>
		public void Disconnect (Exception ex)
		{
			RecordClientDisconnected (ex);

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
			using (var builder = new ByteArrayBuilder (64)) {
				bool complete;

				do {
					complete = Stream.ReadLine (builder, cancellationToken);
				} while (!complete);

				// FIXME: All callers expect CRLF to be trimmed, but many also want all trailing whitespace trimmed.
				builder.TrimNewLine ();

				return builder.ToString ();
			}
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
		public async Task<string> ReadLineAsync (CancellationToken cancellationToken)
		{
			using (var builder = new ByteArrayBuilder (64)) {
				bool complete;

				do {
					complete = await Stream.ReadLineAsync (builder, cancellationToken).ConfigureAwait (false);
				} while (!complete);

				// FIXME: All callers expect CRLF to be trimmed, but many also want all trailing whitespace trimmed.
				builder.TrimNewLine ();

				return builder.ToString ();
			}
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
		public ValueTask<ImapToken> ReadTokenAsync (CancellationToken cancellationToken)
		{
			return Stream.ReadTokenAsync (cancellationToken);
		}

		/// <summary>
		/// Reads the next token.
		/// </summary>
		/// <returns>The token.</returns>
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
		public ImapToken ReadToken (string specials, CancellationToken cancellationToken)
		{
			return Stream.ReadToken (specials, cancellationToken);
		}

		/// <summary>
		/// Asynchronously reads the next token.
		/// </summary>
		/// <returns>The token.</returns>
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
		public ValueTask<ImapToken> ReadTokenAsync (string specials, CancellationToken cancellationToken)
		{
			return Stream.ReadTokenAsync (specials, cancellationToken);
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
			var token = Stream.ReadToken (specials, cancellationToken);

			Stream.UngetToken (token);

			return token;
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
		public async ValueTask<ImapToken> PeekTokenAsync (string specials, CancellationToken cancellationToken)
		{
			var token = await Stream.ReadTokenAsync (specials, cancellationToken).ConfigureAwait (false);

			Stream.UngetToken (token);

			return token;
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
			var token = Stream.ReadToken (cancellationToken);

			Stream.UngetToken (token);

			return token;
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
		public async ValueTask<ImapToken> PeekTokenAsync (CancellationToken cancellationToken)
		{
			var token = await Stream.ReadTokenAsync (cancellationToken).ConfigureAwait (false);

			Stream.UngetToken (token);

			return token;
		}

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
			if (Stream.Mode != ImapStreamMode.Literal)
				throw new InvalidOperationException ();

			int literalLength = Stream.LiteralLength;
			var buf = ArrayPool<byte>.Shared.Rent (literalLength);

			try {
				int n, nread = 0;

				do {
					if ((n = Stream.Read (buf, nread, literalLength - nread, cancellationToken)) > 0)
						nread += n;
				} while (nread < literalLength);

				try {
					return TextEncodings.UTF8.GetString (buf, 0, nread);
				} catch {
					return TextEncodings.Latin1.GetString (buf, 0, nread);
				}
			} finally {
				ArrayPool<byte>.Shared.Return (buf);
			}
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
		public async Task<string> ReadLiteralAsync (CancellationToken cancellationToken)
		{
			if (Stream.Mode != ImapStreamMode.Literal)
				throw new InvalidOperationException ();

			int literalLength = Stream.LiteralLength;
			var buf = ArrayPool<byte>.Shared.Rent (literalLength);

			try {
				int n, nread = 0;

				do {
					if ((n = await Stream.ReadAsync (buf, nread, literalLength - nread, cancellationToken).ConfigureAwait (false)) > 0)
						nread += n;
				} while (nread < literalLength);

				try {
					return TextEncodings.UTF8.GetString (buf, 0, nread);
				} catch {
					return TextEncodings.Latin1.GetString (buf, 0, nread);
				}
			} finally {
				ArrayPool<byte>.Shared.Return (buf);
			}
		}

		void SkipLine (CancellationToken cancellationToken)
		{
			ImapToken token;

			do {
				token = ReadToken (cancellationToken);

				if (token.Type == ImapTokenType.Literal) {
					var buf = ArrayPool<byte>.Shared.Rent (BufferSize);
					int nread;

					try {
						do {
							nread = Stream.Read (buf, 0, BufferSize, cancellationToken);
						} while (nread > 0);
					} finally {
						ArrayPool<byte>.Shared.Return (buf);
					}
				}
			} while (token.Type != ImapTokenType.Eoln);
		}

		async Task SkipLineAsync (CancellationToken cancellationToken)
		{
			ImapToken token;

			do {
				token = await ReadTokenAsync (cancellationToken).ConfigureAwait (false);

				if (token.Type == ImapTokenType.Literal) {
					var buf = ArrayPool<byte>.Shared.Rent (BufferSize);
					int nread;

					try {
						do {
							nread = await Stream.ReadAsync (buf, 0, BufferSize, cancellationToken).ConfigureAwait (false);
						} while (nread > 0);
					} finally {
						ArrayPool<byte>.Shared.Return (buf);
					}
				}
			} while (token.Type != ImapTokenType.Eoln);
		}

		static bool TryParseUInt32 (string text, int startIndex, out uint value)
		{
#if NETSTANDARD2_1_OR_GREATER || NET5_0_OR_GREATER
			var token = text.AsSpan (startIndex);
#else
			var token = text.Substring (startIndex);
#endif

			return uint.TryParse (token, NumberStyles.None, CultureInfo.InvariantCulture, out value);
		}

		void ResetCapabilities ()
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
		}

		void ProcessCapabilityToken (string atom)
		{
			if (atom.StartsWith ("AUTH=", StringComparison.OrdinalIgnoreCase)) {
				AuthenticationMechanisms.Add (atom.Substring ("AUTH=".Length));
			} else if (atom.StartsWith ("APPENDLIMIT", StringComparison.OrdinalIgnoreCase)) {
				if (atom.Length >= "APPENDLIMIT".Length) {
					if (atom.Length >= "APPENDLIMIT=".Length && TryParseUInt32 (atom, "APPENDLIMIT=".Length, out uint limit))
						AppendLimit = limit;

					Capabilities |= ImapCapabilities.AppendLimit;
				}
			} else if (atom.StartsWith ("COMPRESS=", StringComparison.OrdinalIgnoreCase)) {
				CompressionAlgorithms.Add (atom.Substring ("COMPRESS=".Length));
				Capabilities |= ImapCapabilities.Compress;
			} else if (atom.StartsWith ("CONTEXT=", StringComparison.OrdinalIgnoreCase)) {
				SupportedContexts.Add (atom.Substring ("CONTEXT=".Length));
				Capabilities |= ImapCapabilities.Context;
			} else if (atom.StartsWith ("I18NLEVEL=", StringComparison.OrdinalIgnoreCase)) {
				if (TryParseUInt32 (atom, "I18NLEVEL=".Length, out uint level))
					I18NLevel = (int) level;

				Capabilities |= ImapCapabilities.I18NLevel;
			} else if (atom.StartsWith ("RIGHTS=", StringComparison.OrdinalIgnoreCase)) {
				var rights = atom.Substring ("RIGHTS=".Length);
				Rights.AddRange (rights);
			} else if (atom.StartsWith ("THREAD=", StringComparison.OrdinalIgnoreCase)) {
				if (string.Compare ("ORDEREDSUBJECT", 0, atom, "THREAD=".Length, "ORDEREDSUBJECT".Length, StringComparison.OrdinalIgnoreCase) == 0)
					ThreadingAlgorithms.Add (ThreadingAlgorithm.OrderedSubject);
				else if (string.Compare ("REFERENCES", 0, atom, "THREAD=".Length, "REFERENCES".Length, StringComparison.OrdinalIgnoreCase) == 0)
					ThreadingAlgorithms.Add (ThreadingAlgorithm.References);

				Capabilities |= ImapCapabilities.Thread;
			} else if (atom.Equals ("IMAP4", StringComparison.OrdinalIgnoreCase)) {
				Capabilities |= ImapCapabilities.IMAP4;
			} else if (atom.Equals ("IMAP4REV1", StringComparison.OrdinalIgnoreCase)) {
				Capabilities |= ImapCapabilities.IMAP4rev1;
			} else if (atom.Equals ("IMAP4REV2", StringComparison.OrdinalIgnoreCase)) {
				Capabilities |= ImapCapabilities.IMAP4rev2;
			} else if (atom.Equals ("STATUS", StringComparison.OrdinalIgnoreCase)) {
				Capabilities |= ImapCapabilities.Status;
			} else if (atom.Equals ("ACL", StringComparison.OrdinalIgnoreCase)) {
				Capabilities |= ImapCapabilities.Acl;
			} else if (atom.Equals ("QUOTA", StringComparison.OrdinalIgnoreCase)) {
				Capabilities |= ImapCapabilities.Quota;
			} else if (atom.Equals ("LITERAL+", StringComparison.OrdinalIgnoreCase)) {
				Capabilities |= ImapCapabilities.LiteralPlus;
			} else if (atom.Equals ("IDLE", StringComparison.OrdinalIgnoreCase)) {
				Capabilities |= ImapCapabilities.Idle;
			} else if (atom.Equals ("MAILBOX-REFERRALS", StringComparison.OrdinalIgnoreCase)) {
				Capabilities |= ImapCapabilities.MailboxReferrals;
			} else if (atom.Equals ("LOGIN-REFERRALS", StringComparison.OrdinalIgnoreCase)) {
				Capabilities |= ImapCapabilities.LoginReferrals;
			} else if (atom.Equals ("NAMESPACE", StringComparison.OrdinalIgnoreCase)) {
				Capabilities |= ImapCapabilities.Namespace;
			} else if (atom.Equals ("ID", StringComparison.OrdinalIgnoreCase)) {
				Capabilities |= ImapCapabilities.Id;
			} else if (atom.Equals ("CHILDREN", StringComparison.OrdinalIgnoreCase)) {
				Capabilities |= ImapCapabilities.Children;
			} else if (atom.Equals ("LOGINDISABLED", StringComparison.OrdinalIgnoreCase)) {
				Capabilities |= ImapCapabilities.LoginDisabled;
			} else if (atom.Equals ("STARTTLS", StringComparison.OrdinalIgnoreCase)) {
				Capabilities |= ImapCapabilities.StartTLS;
			} else if (atom.Equals ("MULTIAPPEND", StringComparison.OrdinalIgnoreCase)) {
				Capabilities |= ImapCapabilities.MultiAppend;
			} else if (atom.Equals ("BINARY", StringComparison.OrdinalIgnoreCase)) {
				Capabilities |= ImapCapabilities.Binary;
			} else if (atom.Equals ("UNSELECT", StringComparison.OrdinalIgnoreCase)) {
				Capabilities |= ImapCapabilities.Unselect;
			} else if (atom.Equals ("UIDPLUS", StringComparison.OrdinalIgnoreCase)) {
				Capabilities |= ImapCapabilities.UidPlus;
			} else if (atom.Equals ("CATENATE", StringComparison.OrdinalIgnoreCase)) {
				Capabilities |= ImapCapabilities.Catenate;
			} else if (atom.Equals ("CONDSTORE", StringComparison.OrdinalIgnoreCase)) {
				Capabilities |= ImapCapabilities.CondStore;
			} else if (atom.Equals ("ESEARCH", StringComparison.OrdinalIgnoreCase)) {
				Capabilities |= ImapCapabilities.ESearch;
			} else if (atom.Equals ("SASL-IR", StringComparison.OrdinalIgnoreCase)) {
				Capabilities |= ImapCapabilities.SaslIR;
			} else if (atom.Equals ("WITHIN", StringComparison.OrdinalIgnoreCase)) {
				Capabilities |= ImapCapabilities.Within;
			} else if (atom.Equals ("ENABLE", StringComparison.OrdinalIgnoreCase)) {
				Capabilities |= ImapCapabilities.Enable;
			} else if (atom.Equals ("QRESYNC", StringComparison.OrdinalIgnoreCase)) {
				Capabilities |= ImapCapabilities.QuickResync;
			} else if (atom.Equals ("SEARCHRES", StringComparison.OrdinalIgnoreCase)) {
				Capabilities |= ImapCapabilities.SearchResults;
			} else if (atom.Equals ("SORT", StringComparison.OrdinalIgnoreCase)) {
				Capabilities |= ImapCapabilities.Sort;
			} else if (atom.Equals ("ANNOTATE-EXPERIMENT-1", StringComparison.OrdinalIgnoreCase)) {
				Capabilities |= ImapCapabilities.Annotate;
			} else if (atom.Equals ("LIST-EXTENDED", StringComparison.OrdinalIgnoreCase)) {
				Capabilities |= ImapCapabilities.ListExtended;
			} else if (atom.Equals ("CONVERT", StringComparison.OrdinalIgnoreCase)) {
				Capabilities |= ImapCapabilities.Convert;
			} else if (atom.Equals ("LANGUAGE", StringComparison.OrdinalIgnoreCase)) {
				Capabilities |= ImapCapabilities.Language;
			} else if (atom.Equals ("ESORT", StringComparison.OrdinalIgnoreCase)) {
				Capabilities |= ImapCapabilities.ESort;
			} else if (atom.Equals ("METADATA", StringComparison.OrdinalIgnoreCase)) {
				Capabilities |= ImapCapabilities.Metadata;
			} else if (atom.Equals ("METADATA-SERVER", StringComparison.OrdinalIgnoreCase)) {
				Capabilities |= ImapCapabilities.MetadataServer;
			} else if (atom.Equals ("NOTIFY", StringComparison.OrdinalIgnoreCase)) {
				Capabilities |= ImapCapabilities.Notify;
			} else if (atom.Equals ("FILTERS", StringComparison.OrdinalIgnoreCase)) {
				Capabilities |= ImapCapabilities.Filters;
			} else if (atom.Equals ("LIST-STATUS", StringComparison.OrdinalIgnoreCase)) {
				Capabilities |= ImapCapabilities.ListStatus;
			} else if (atom.Equals ("SORT=DISPLAY", StringComparison.OrdinalIgnoreCase)) {
				Capabilities |= ImapCapabilities.SortDisplay;
			} else if (atom.Equals ("CREATE-SPECIAL-USE", StringComparison.OrdinalIgnoreCase)) {
				Capabilities |= ImapCapabilities.CreateSpecialUse;
			} else if (atom.Equals ("SPECIAL-USE", StringComparison.OrdinalIgnoreCase)) {
				Capabilities |= ImapCapabilities.SpecialUse;
			} else if (atom.Equals ("SEARCH=FUZZY", StringComparison.OrdinalIgnoreCase)) {
				Capabilities |= ImapCapabilities.FuzzySearch;
			} else if (atom.Equals ("MULTISEARCH", StringComparison.OrdinalIgnoreCase)) {
				Capabilities |= ImapCapabilities.MultiSearch;
			} else if (atom.Equals ("MOVE", StringComparison.OrdinalIgnoreCase)) {
				Capabilities |= ImapCapabilities.Move;
			} else if (atom.Equals ("UTF8=ACCEPT", StringComparison.OrdinalIgnoreCase)) {
				Capabilities |= ImapCapabilities.UTF8Accept;
			} else if (atom.Equals ("UTF8=ONLY", StringComparison.OrdinalIgnoreCase)) {
				Capabilities |= ImapCapabilities.UTF8Only;
			} else if (atom.Equals ("LITERAL-", StringComparison.OrdinalIgnoreCase)) {
				Capabilities |= ImapCapabilities.LiteralMinus;
			} else if (atom.Equals ("UNAUTHENTICATE", StringComparison.OrdinalIgnoreCase)) {
				Capabilities |= ImapCapabilities.Unauthenticate;
			} else if (atom.Equals ("STATUS=SIZE", StringComparison.OrdinalIgnoreCase)) {
				Capabilities |= ImapCapabilities.StatusSize;
			} else if (atom.Equals ("LIST-MYRIGHTS", StringComparison.OrdinalIgnoreCase)) {
				Capabilities |= ImapCapabilities.ListMyRights;
			} else if (atom.Equals ("OBJECTID", StringComparison.OrdinalIgnoreCase)) {
				Capabilities |= ImapCapabilities.ObjectID;
			} else if (atom.Equals ("REPLACE", StringComparison.OrdinalIgnoreCase)) {
				Capabilities |= ImapCapabilities.Replace;
			} else if (atom.Equals ("SAVEDATE", StringComparison.OrdinalIgnoreCase)) {
				Capabilities |= ImapCapabilities.SaveDate;
			} else if (atom.Equals ("PREVIEW", StringComparison.OrdinalIgnoreCase)) {
				Capabilities |= ImapCapabilities.Preview;
			} else if (atom.Equals ("XLIST", StringComparison.OrdinalIgnoreCase)) {
				Capabilities |= ImapCapabilities.XList;
			} else if (atom.Equals ("X-GM-EXT-1", StringComparison.OrdinalIgnoreCase)) {
				Capabilities |= ImapCapabilities.GMailExt1;
				QuirksMode = ImapQuirksMode.GMail;
			} else if (atom.Equals ("XSTOP", StringComparison.OrdinalIgnoreCase)) {
				QuirksMode = ImapQuirksMode.ProtonMail;
			} else if (atom.Equals ("X-SUN-IMAP", StringComparison.OrdinalIgnoreCase)) {
				QuirksMode = ImapQuirksMode.SunMicrosystems;
			} else if (atom.Equals ("XYMHIGHESTMODSEQ", StringComparison.OrdinalIgnoreCase)) {
				QuirksMode = ImapQuirksMode.Yahoo;
			}
		}

		void StandardizeCapabilities ()
		{
			if ((Capabilities & ImapCapabilities.IMAP4rev2) != 0) {
				ProtocolVersion = ImapProtocolVersion.IMAP4rev2;

				// Rfc9051, Appendix E defines the capabilities that IMAP4rev2 should be assumed to implement:
				Capabilities |= ImapCapabilities.Status |
					ImapCapabilities.Namespace | ImapCapabilities.Unselect | ImapCapabilities.UidPlus | ImapCapabilities.ESearch |
					ImapCapabilities.SearchResults | ImapCapabilities.Enable | ImapCapabilities.Idle | ImapCapabilities.SaslIR | ImapCapabilities.ListExtended |
					ImapCapabilities.ListStatus | ImapCapabilities.Move | ImapCapabilities.LiteralMinus | ImapCapabilities.SpecialUse;

				// Note: IMAP4rev2 also supports the FETCH portion of the 'BINARY' extension but not the APPEND portion. Since
				// we currently have no way to distinguish between them using the ImapCapabilities enum, we do not enable the
				// ImapCapabilities.Binary extension flag.
			} else if ((Capabilities & ImapCapabilities.IMAP4rev1) != 0) {
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

		void UpdateCapabilities (ImapTokenType sentinel, CancellationToken cancellationToken)
		{
			ResetCapabilities ();

			var token = ReadToken (cancellationToken);

			// Note: Some buggy IMAP servers mistakenly put a space between "LITERAL" and "+" which causes our tokenizer to read
			// a '+' token thereby causing an "Unexpected token: '+'" exception to be thrown. If we treat '+' tokens as atoms
			// like we did in v4.1.0 (and older), then we can avoid this exception.
			//
			// See https://github.com/jstedfast/MailKit/issues/1654 for details.
			while (token.Type == ImapTokenType.Atom || token.Type == ImapTokenType.Plus) {
				var atom = token.Value.ToString ();

				ProcessCapabilityToken (atom);

				token = ReadToken (cancellationToken);
			}

			AssertToken (token, sentinel, GenericItemSyntaxErrorFormat, "CAPABILITIES", token);

			// unget the sentinel
			Stream.UngetToken (token);

			StandardizeCapabilities ();
		}

		async Task UpdateCapabilitiesAsync (ImapTokenType sentinel, CancellationToken cancellationToken)
		{
			ResetCapabilities ();

			var token = await ReadTokenAsync (cancellationToken).ConfigureAwait (false);

			// Note: Some buggy IMAP servers mistakenly put a space between "LITERAL" and "+" which causes our tokenizer to read
			// a '+' token thereby causing an "Unexpected token: '+'" exception to be thrown. If we treat '+' tokens as atoms
			// like we did in v4.1.0 (and older), then we can avoid this exception.
			//
			// See https://github.com/jstedfast/MailKit/issues/1654 for details.
			while (token.Type == ImapTokenType.Atom || token.Type == ImapTokenType.Plus) {
				var atom = token.Value.ToString ();

				ProcessCapabilityToken (atom);

				token = await ReadTokenAsync (cancellationToken).ConfigureAwait (false);
			}

			AssertToken (token, sentinel, GenericItemSyntaxErrorFormat, "CAPABILITIES", token);

			// unget the sentinel
			Stream.UngetToken (token);

			StandardizeCapabilities ();
		}

		void UpdateNamespaces (CancellationToken cancellationToken)
		{
			var namespaces = new List<FolderNamespaceCollection> {
				PersonalNamespaces, OtherNamespaces, SharedNamespaces
			};
			ImapToken token;
			string path;
			char delim;
			int n = 0;

			PersonalNamespaces.Clear ();
			SharedNamespaces.Clear ();
			OtherNamespaces.Clear ();

			token = ReadToken (cancellationToken);

			do {
				if (token.Type == ImapTokenType.OpenParen) {
					// parse the list of namespace pairs...
					token = ReadToken (cancellationToken);

					while (token.Type == ImapTokenType.OpenParen) {
						// parse the namespace pair - first token is the path
						token = ReadToken (cancellationToken);

						AssertToken (token, ImapTokenType.Atom, ImapTokenType.QString, GenericUntaggedResponseSyntaxErrorFormat, "NAMESPACE", token);

						path = (string) token.Value;

						// second token is the directory separator
						token = ReadToken (cancellationToken);

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

						if (!TryGetCachedFolder (path, out var folder)) {
							folder = CreateImapFolder (path, FolderAttributes.None, delim);
							CacheFolder (folder);
						}

						folder.UpdateIsNamespace (true);

						do {
							token = ReadToken (cancellationToken);

							if (token.Type == ImapTokenType.CloseParen)
								break;

							// NAMESPACE extension

							AssertToken (token, ImapTokenType.Atom, ImapTokenType.QString, GenericUntaggedResponseSyntaxErrorFormat, "NAMESPACE", token);

							token = ReadToken (cancellationToken);

							AssertToken (token, ImapTokenType.OpenParen, GenericUntaggedResponseSyntaxErrorFormat, "NAMESPACE", token);

							do {
								token = ReadToken (cancellationToken);

								if (token.Type == ImapTokenType.CloseParen)
									break;

								AssertToken (token, ImapTokenType.Atom, ImapTokenType.QString, GenericUntaggedResponseSyntaxErrorFormat, "NAMESPACE", token);
							} while (true);
						} while (true);

						// read the next token - it should either be '(' or ')'
						token = ReadToken (cancellationToken);
					}

					AssertToken (token, ImapTokenType.CloseParen, GenericUntaggedResponseSyntaxErrorFormat, "NAMESPACE", token);
				} else {
					AssertToken (token, ImapTokenType.Nil, GenericUntaggedResponseSyntaxErrorFormat, "NAMESPACE", token);
				}

				token = ReadToken (cancellationToken);
				n++;
			} while (n < 3);

			while (token.Type != ImapTokenType.Eoln)
				token = ReadToken (cancellationToken);
		}

		async ValueTask UpdateNamespacesAsync (CancellationToken cancellationToken)
		{
			var namespaces = new List<FolderNamespaceCollection> {
				PersonalNamespaces, OtherNamespaces, SharedNamespaces
			};
			ImapToken token;
			string path;
			char delim;
			int n = 0;

			PersonalNamespaces.Clear ();
			SharedNamespaces.Clear ();
			OtherNamespaces.Clear ();

			token = await ReadTokenAsync (cancellationToken).ConfigureAwait (false);

			do {
				if (token.Type == ImapTokenType.OpenParen) {
					// parse the list of namespace pairs...
					token = await ReadTokenAsync (cancellationToken).ConfigureAwait (false);

					while (token.Type == ImapTokenType.OpenParen) {
						// parse the namespace pair - first token is the path
						token = await ReadTokenAsync (cancellationToken).ConfigureAwait (false);

						AssertToken (token, ImapTokenType.Atom, ImapTokenType.QString, GenericUntaggedResponseSyntaxErrorFormat, "NAMESPACE", token);

						path = (string) token.Value;

						// second token is the directory separator
						token = await ReadTokenAsync (cancellationToken).ConfigureAwait (false);

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

						if (!TryGetCachedFolder (path, out var folder)) {
							folder = CreateImapFolder (path, FolderAttributes.None, delim);
							CacheFolder (folder);
						}

						folder.UpdateIsNamespace (true);

						do {
							token = await ReadTokenAsync (cancellationToken).ConfigureAwait (false);

							if (token.Type == ImapTokenType.CloseParen)
								break;

							// NAMESPACE extension

							AssertToken (token, ImapTokenType.Atom, ImapTokenType.QString, GenericUntaggedResponseSyntaxErrorFormat, "NAMESPACE", token);

							token = await ReadTokenAsync (cancellationToken).ConfigureAwait (false);

							AssertToken (token, ImapTokenType.OpenParen, GenericUntaggedResponseSyntaxErrorFormat, "NAMESPACE", token);

							do {
								token = await ReadTokenAsync (cancellationToken).ConfigureAwait (false);

								if (token.Type == ImapTokenType.CloseParen)
									break;

								AssertToken (token, ImapTokenType.Atom, ImapTokenType.QString, GenericUntaggedResponseSyntaxErrorFormat, "NAMESPACE", token);
							} while (true);
						} while (true);

						// read the next token - it should either be '(' or ')'
						token = await ReadTokenAsync (cancellationToken).ConfigureAwait (false);
					}

					AssertToken (token, ImapTokenType.CloseParen, GenericUntaggedResponseSyntaxErrorFormat, "NAMESPACE", token);
				} else {
					AssertToken (token, ImapTokenType.Nil, GenericUntaggedResponseSyntaxErrorFormat, "NAMESPACE", token);
				}

				token = await ReadTokenAsync (cancellationToken).ConfigureAwait (false);
				n++;
			} while (n < 3);

			while (token.Type != ImapTokenType.Eoln)
				token = await ReadTokenAsync (cancellationToken).ConfigureAwait (false);
		}

		void ProcessResponseCodes (ImapCommand ic)
		{
			foreach (var code in ic.RespCodes) {
				switch (code.Type) {
				case ImapResponseCodeType.Alert:
					OnAlert (code.Message);
					break;
				case ImapResponseCodeType.WebAlert:
					OnWebAlert (((WebAlertResponseCode) code).WebUri, code.Message);
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

			if (encodedName.Length == 0) {
				OnMetadataChanged (metadata);
			} else if (FolderCache.TryGetValue (encodedName, out var folder)) {
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
			if (atom.Equals ("ALERT", StringComparison.OrdinalIgnoreCase))
				return ImapResponseCodeType.Alert;
			if (atom.Equals ("BADCHARSET", StringComparison.OrdinalIgnoreCase))
				return ImapResponseCodeType.BadCharset;
			if (atom.Equals ("CAPABILITY", StringComparison.OrdinalIgnoreCase))
				return ImapResponseCodeType.Capability;
			if (atom.Equals ("NEWNAME", StringComparison.OrdinalIgnoreCase))
				return ImapResponseCodeType.NewName;
			if (atom.Equals ("PARSE", StringComparison.OrdinalIgnoreCase))
				return ImapResponseCodeType.Parse;
			if (atom.Equals ("PERMANENTFLAGS", StringComparison.OrdinalIgnoreCase))
				return ImapResponseCodeType.PermanentFlags;
			if (atom.Equals ("READ-ONLY", StringComparison.OrdinalIgnoreCase))
				return ImapResponseCodeType.ReadOnly;
			if (atom.Equals ("READ-WRITE", StringComparison.OrdinalIgnoreCase))
				return ImapResponseCodeType.ReadWrite;
			if (atom.Equals ("TRYCREATE", StringComparison.OrdinalIgnoreCase))
				return ImapResponseCodeType.TryCreate;
			if (atom.Equals ("UIDNEXT", StringComparison.OrdinalIgnoreCase))
				return ImapResponseCodeType.UidNext;
			if (atom.Equals ("UIDVALIDITY", StringComparison.OrdinalIgnoreCase))
				return ImapResponseCodeType.UidValidity;
			if (atom.Equals ("UNSEEN", StringComparison.OrdinalIgnoreCase))
				return ImapResponseCodeType.Unseen;
			if (atom.Equals ("REFERRAL", StringComparison.OrdinalIgnoreCase))
				return ImapResponseCodeType.Referral;
			if (atom.Equals ("UNKNOWN-CTE", StringComparison.OrdinalIgnoreCase))
				return ImapResponseCodeType.UnknownCte;
			if (atom.Equals ("APPENDUID", StringComparison.OrdinalIgnoreCase))
				return ImapResponseCodeType.AppendUid;
			if (atom.Equals ("COPYUID", StringComparison.OrdinalIgnoreCase))
				return ImapResponseCodeType.CopyUid;
			if (atom.Equals ("UIDNOTSTICKY", StringComparison.OrdinalIgnoreCase))
				return ImapResponseCodeType.UidNotSticky;
			if (atom.Equals ("URLMECH", StringComparison.OrdinalIgnoreCase))
				return ImapResponseCodeType.UrlMech;
			if (atom.Equals ("BADURL", StringComparison.OrdinalIgnoreCase))
				return ImapResponseCodeType.BadUrl;
			if (atom.Equals ("TOOBIG", StringComparison.OrdinalIgnoreCase))
				return ImapResponseCodeType.TooBig;
			if (atom.Equals ("HIGHESTMODSEQ", StringComparison.OrdinalIgnoreCase))
				return ImapResponseCodeType.HighestModSeq;
			if (atom.Equals ("MODIFIED", StringComparison.OrdinalIgnoreCase))
				return ImapResponseCodeType.Modified;
			if (atom.Equals ("NOMODSEQ", StringComparison.OrdinalIgnoreCase))
				return ImapResponseCodeType.NoModSeq;
			if (atom.Equals ("COMPRESSIONACTIVE", StringComparison.OrdinalIgnoreCase))
				return ImapResponseCodeType.CompressionActive;
			if (atom.Equals ("CLOSED", StringComparison.OrdinalIgnoreCase))
				return ImapResponseCodeType.Closed;
			if (atom.Equals ("NOTSAVED", StringComparison.OrdinalIgnoreCase))
				return ImapResponseCodeType.NotSaved;
			if (atom.Equals ("BADCOMPARATOR", StringComparison.OrdinalIgnoreCase))
				return ImapResponseCodeType.BadComparator;
			if (atom.Equals ("ANNOTATE", StringComparison.OrdinalIgnoreCase))
				return ImapResponseCodeType.Annotate;
			if (atom.Equals ("ANNOTATIONS", StringComparison.OrdinalIgnoreCase))
				return ImapResponseCodeType.Annotations;
			if (atom.Equals ("MAXCONVERTMESSAGES", StringComparison.OrdinalIgnoreCase))
				return ImapResponseCodeType.MaxConvertMessages;
			if (atom.Equals ("MAXCONVERTPARTS", StringComparison.OrdinalIgnoreCase))
				return ImapResponseCodeType.MaxConvertParts;
			if (atom.Equals ("TEMPFAIL", StringComparison.OrdinalIgnoreCase))
				return ImapResponseCodeType.TempFail;
			if (atom.Equals ("NOUPDATE", StringComparison.OrdinalIgnoreCase))
				return ImapResponseCodeType.NoUpdate;
			if (atom.Equals ("METADATA", StringComparison.OrdinalIgnoreCase))
				return ImapResponseCodeType.Metadata;
			if (atom.Equals ("NOTIFICATIONOVERFLOW", StringComparison.OrdinalIgnoreCase))
				return ImapResponseCodeType.NotificationOverflow;
			if (atom.Equals ("BADEVENT", StringComparison.OrdinalIgnoreCase))
				return ImapResponseCodeType.BadEvent;
			if (atom.Equals ("UNDEFINED-FILTER", StringComparison.OrdinalIgnoreCase))
				return ImapResponseCodeType.UndefinedFilter;
			if (atom.Equals ("UNAVAILABLE", StringComparison.OrdinalIgnoreCase))
				return ImapResponseCodeType.Unavailable;
			if (atom.Equals ("AUTHENTICATIONFAILED", StringComparison.OrdinalIgnoreCase))
				return ImapResponseCodeType.AuthenticationFailed;
			if (atom.Equals ("AUTHORIZATIONFAILED", StringComparison.OrdinalIgnoreCase))
				return ImapResponseCodeType.AuthorizationFailed;
			if (atom.Equals ("EXPIRED", StringComparison.OrdinalIgnoreCase))
				return ImapResponseCodeType.Expired;
			if (atom.Equals ("PRIVACYREQUIRED", StringComparison.OrdinalIgnoreCase))
				return ImapResponseCodeType.PrivacyRequired;
			if (atom.Equals ("CONTACTADMIN", StringComparison.OrdinalIgnoreCase))
				return ImapResponseCodeType.ContactAdmin;
			if (atom.Equals ("NOPERM", StringComparison.OrdinalIgnoreCase))
				return ImapResponseCodeType.NoPerm;
			if (atom.Equals ("INUSE", StringComparison.OrdinalIgnoreCase))
				return ImapResponseCodeType.InUse;
			if (atom.Equals ("EXPUNGEISSUED", StringComparison.OrdinalIgnoreCase))
				return ImapResponseCodeType.ExpungeIssued;
			if (atom.Equals ("CORRUPTION", StringComparison.OrdinalIgnoreCase))
				return ImapResponseCodeType.Corruption;
			if (atom.Equals ("SERVERBUG", StringComparison.OrdinalIgnoreCase))
				return ImapResponseCodeType.ServerBug;
			if (atom.Equals ("CLIENTBUG", StringComparison.OrdinalIgnoreCase))
				return ImapResponseCodeType.ClientBug;
			if (atom.Equals ("CANNOT", StringComparison.OrdinalIgnoreCase))
				return ImapResponseCodeType.CanNot;
			if (atom.Equals ("LIMIT", StringComparison.OrdinalIgnoreCase))
				return ImapResponseCodeType.Limit;
			if (atom.Equals ("OVERQUOTA", StringComparison.OrdinalIgnoreCase))
				return ImapResponseCodeType.OverQuota;
			if (atom.Equals ("ALREADYEXISTS", StringComparison.OrdinalIgnoreCase))
				return ImapResponseCodeType.AlreadyExists;
			if (atom.Equals ("NONEXISTENT", StringComparison.OrdinalIgnoreCase))
				return ImapResponseCodeType.NonExistent;
			if (atom.Equals ("USEATTR", StringComparison.OrdinalIgnoreCase))
				return ImapResponseCodeType.UseAttr;
			if (atom.Equals ("MAILBOXID", StringComparison.OrdinalIgnoreCase))
				return ImapResponseCodeType.MailboxId;
			if (atom.Equals ("WEBALERT", StringComparison.OrdinalIgnoreCase))
				return ImapResponseCodeType.WebAlert;

			return ImapResponseCodeType.Unknown;
		}

		/// <summary>
		/// Parses the response code.
		/// </summary>
		/// <returns>The response code.</returns>
		/// <param name="isTagged">Whether or not the resp-code is tagged vs untagged.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		public ImapResponseCode ParseResponseCode (bool isTagged, CancellationToken cancellationToken)
		{
			uint validity = Selected != null ? Selected.UidValidity : 0;
			ImapResponseCode code;
			string atom, value;
			ImapToken token;

			//			token = ReadToken (cancellationToken);
			//
			//			if (token.Type != ImapTokenType.LeftBracket) {
			//				Debug.WriteLine ("Expected a '[' followed by a RESP-CODE, but got: {0}", token);
			//				throw UnexpectedToken (token, false);
			//			}

			token = ReadToken (cancellationToken);

			AssertToken (token, ImapTokenType.Atom, "Syntax error in response code. {0}", token);

			atom = (string) token.Value;
			token = ReadToken (cancellationToken);

			code = ImapResponseCode.Create (GetResponseCodeType (atom));
			code.IsTagged = isTagged;

			switch (code.Type) {
			case ImapResponseCodeType.BadCharset:
				if (token.Type == ImapTokenType.OpenParen) {
					token = ReadToken (cancellationToken);

					SupportedCharsets.Clear ();
					while (token.Type == ImapTokenType.Atom || token.Type == ImapTokenType.QString) {
						SupportedCharsets.Add ((string) token.Value);
						token = ReadToken (cancellationToken);
					}

					AssertToken (token, ImapTokenType.CloseParen, GenericResponseCodeSyntaxErrorFormat, "BADCHARSET", token);

					token = ReadToken (cancellationToken);
				}
				break;
			case ImapResponseCodeType.Capability:
				Stream.UngetToken (token);
				UpdateCapabilities (ImapTokenType.CloseBracket, cancellationToken);
				token = ReadToken (cancellationToken);
				break;
			case ImapResponseCodeType.PermanentFlags:
				var perm = (PermanentFlagsResponseCode) code;

				Stream.UngetToken (token);
				perm.Flags = ImapUtils.ParseFlagsList (this, "PERMANENTFLAGS", perm.Keywords, cancellationToken);
				token = ReadToken (cancellationToken);
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
				token = ReadToken (cancellationToken);
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
				token = ReadToken (cancellationToken);
				break;
			case ImapResponseCodeType.Unseen:
				var unseen = (UnseenResponseCode) code;

				// Note: we allow '0' here because some servers have been known to send "* OK [UNSEEN 0]" when the
				// mailbox contains no messages.
				//
				// See https://github.com/jstedfast/MailKit/issues/34 for details.
				var n = ParseNumber (token, false, GenericResponseCodeSyntaxErrorFormat, "UNSEEN", token);

				unseen.Index = n > 0 ? (int) (n - 1) : 0;

				token = ReadToken (cancellationToken);
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
				token = ReadToken (cancellationToken);

				AssertToken (token, ImapTokenType.Atom, ImapTokenType.QString, GenericResponseCodeSyntaxErrorFormat, "NEWNAME", token);

				rename.NewName = (string) token.Value;

				token = ReadToken (cancellationToken);
				break;
			case ImapResponseCodeType.AppendUid:
				var append = (AppendUidResponseCode) code;

				append.UidValidity = ParseNumber (token, false, GenericResponseCodeSyntaxErrorFormat, "APPENDUID", token);

				token = ReadToken (cancellationToken);

				// The MULTIAPPEND extension redefines APPENDUID's second argument to be a uid-set instead of a single uid.
				append.UidSet = ParseUidSet (token, append.UidValidity, out _, out _, GenericResponseCodeSyntaxErrorFormat, "APPENDUID", token);

				token = ReadToken (cancellationToken);
				break;
			case ImapResponseCodeType.CopyUid:
				var copy = (CopyUidResponseCode) code;

				copy.UidValidity = ParseNumber (token, false, GenericResponseCodeSyntaxErrorFormat, "COPYUID", token);

				token = ReadToken (cancellationToken);

				// Note: Outlook.com will apparently sometimes issue a [COPYUID nz_number SPACE SPACE] resp-code
				// in response to a UID COPY or UID MOVE command. Likely this happens only when the source message
				// didn't exist or something? See https://github.com/jstedfast/MailKit/issues/555 for details.

				if (token.Type != ImapTokenType.CloseBracket) {
					copy.SrcUidSet = ParseUidSet (token, validity, out _, out _, GenericResponseCodeSyntaxErrorFormat, "COPYUID", token);
				} else {
					copy.SrcUidSet = new UniqueIdSet ();
					Stream.UngetToken (token);
				}

				token = ReadToken (cancellationToken);

				if (token.Type != ImapTokenType.CloseBracket) {
					copy.DestUidSet = ParseUidSet (token, copy.UidValidity, out _, out _, GenericResponseCodeSyntaxErrorFormat, "COPYUID", token);
				} else {
					copy.DestUidSet = new UniqueIdSet ();
					Stream.UngetToken (token);
				}

				token = ReadToken (cancellationToken);
				break;
			case ImapResponseCodeType.BadUrl:
				var badurl = (BadUrlResponseCode) code;

				AssertToken (token, ImapTokenType.Atom, ImapTokenType.QString, GenericResponseCodeSyntaxErrorFormat, "BADURL", token);

				badurl.BadUrl = (string) token.Value;

				token = ReadToken (cancellationToken);
				break;
			case ImapResponseCodeType.HighestModSeq:
				var highest = (HighestModSeqResponseCode) code;

				highest.HighestModSeq = ParseNumber64 (token, false, GenericResponseCodeSyntaxErrorFormat, "HIGHESTMODSEQ", token);

				token = ReadToken (cancellationToken);
				break;
			case ImapResponseCodeType.Modified:
				var modified = (ModifiedResponseCode) code;

				modified.UidSet = ParseUidSet (token, validity, out _, out _, GenericResponseCodeSyntaxErrorFormat, "MODIFIED", token);

				token = ReadToken (cancellationToken);
				break;
			case ImapResponseCodeType.MaxConvertMessages:
			case ImapResponseCodeType.MaxConvertParts:
				var maxConvert = (MaxConvertResponseCode) code;

				maxConvert.MaxConvert = ParseNumber (token, false, GenericResponseCodeSyntaxErrorFormat, atom, token);

				token = ReadToken (cancellationToken);
				break;
			case ImapResponseCodeType.NoUpdate:
				var noUpdate = (NoUpdateResponseCode) code;

				AssertToken (token, ImapTokenType.Atom, ImapTokenType.QString, GenericResponseCodeSyntaxErrorFormat, "NOUPDATE", token);

				noUpdate.Tag = (string) token.Value;

				token = ReadToken (cancellationToken);
				break;
			case ImapResponseCodeType.Annotate:
				var annotate = (AnnotateResponseCode) code;

				AssertToken (token, ImapTokenType.Atom, GenericResponseCodeSyntaxErrorFormat, "ANNOTATE", token);

				value = (string) token.Value;
				if (value.Equals ("TOOBIG", StringComparison.OrdinalIgnoreCase))
					annotate.SubType = AnnotateResponseCodeSubType.TooBig;
				else if (value.Equals ("TOOMANY", StringComparison.OrdinalIgnoreCase))
					annotate.SubType = AnnotateResponseCodeSubType.TooMany;

				token = ReadToken (cancellationToken);
				break;
			case ImapResponseCodeType.Annotations:
				var annotations = (AnnotationsResponseCode) code;

				AssertToken (token, ImapTokenType.Atom, GenericResponseCodeSyntaxErrorFormat, "ANNOTATIONS", token);

				value = (string) token.Value;
				if (value.Equals ("NONE", StringComparison.OrdinalIgnoreCase)) {
					// nothing
				} else if (value.Equals ("READ-ONLY", StringComparison.OrdinalIgnoreCase)) {
					annotations.Access = AnnotationAccess.ReadOnly;
				} else {
					annotations.Access = AnnotationAccess.ReadWrite;
					annotations.MaxSize = ParseNumber (token, false, GenericResponseCodeSyntaxErrorFormat, "ANNOTATIONS", token);
				}

				token = ReadToken (cancellationToken);

				if (annotations.Access != AnnotationAccess.None) {
					annotations.Scopes = AnnotationScope.Both;

					if (token.Type != ImapTokenType.CloseBracket) {
						AssertToken (token, ImapTokenType.Atom, GenericResponseCodeSyntaxErrorFormat, "ANNOTATIONS", token);

						if (((string) token.Value).Equals ("NOPRIVATE", StringComparison.OrdinalIgnoreCase))
							annotations.Scopes = AnnotationScope.Shared;

						token = ReadToken (cancellationToken);
					}
				}

				break;
			case ImapResponseCodeType.Metadata:
				var metadata = (MetadataResponseCode) code;

				AssertToken (token, ImapTokenType.Atom, GenericResponseCodeSyntaxErrorFormat, "METADATA", token);

				value = (string) token.Value;
				if (value.Equals ("LONGENTRIES", StringComparison.OrdinalIgnoreCase)) {
					metadata.SubType = MetadataResponseCodeSubType.LongEntries;
					metadata.IsError = false;

					token = ReadToken (cancellationToken);

					metadata.Value = ParseNumber (token, false, GenericResponseCodeSyntaxErrorFormat, "METADATA LONGENTRIES", token);
				} else if (value.Equals ("MAXSIZE", StringComparison.OrdinalIgnoreCase)) {
					metadata.SubType = MetadataResponseCodeSubType.MaxSize;

					token = ReadToken (cancellationToken);

					metadata.Value = ParseNumber (token, false, GenericResponseCodeSyntaxErrorFormat, "METADATA MAXSIZE", token);
				} else if (value.Equals ("TOOMANY", StringComparison.OrdinalIgnoreCase)) {
					metadata.SubType = MetadataResponseCodeSubType.TooMany;
				} else if (value.Equals ("NOPRIVATE", StringComparison.OrdinalIgnoreCase)) {
					metadata.SubType = MetadataResponseCodeSubType.NoPrivate;
				}

				token = ReadToken (cancellationToken);
				break;
			case ImapResponseCodeType.UndefinedFilter:
				var undefined = (UndefinedFilterResponseCode) code;

				AssertToken (token, ImapTokenType.Atom, GenericResponseCodeSyntaxErrorFormat, "UNDEFINED-FILTER", token);

				undefined.Name = (string) token.Value;

				token = ReadToken (cancellationToken);
				break;
			case ImapResponseCodeType.MailboxId:
				var mailboxid = (MailboxIdResponseCode) code;

				AssertToken (token, ImapTokenType.OpenParen, GenericResponseCodeSyntaxErrorFormat, "MAILBOXID", token);

				token = ReadToken (cancellationToken);

				AssertToken (token, ImapTokenType.Atom, GenericResponseCodeSyntaxErrorFormat, "MAILBOXID", token);

				mailboxid.MailboxId = (string) token.Value;

				token = ReadToken (cancellationToken);

				AssertToken (token, ImapTokenType.CloseParen, GenericResponseCodeSyntaxErrorFormat, "MAILBOXID", token);

				token = ReadToken (cancellationToken);
				break;
			case ImapResponseCodeType.WebAlert:
				var webalert = (WebAlertResponseCode) code;

				AssertToken (token, ImapTokenType.Atom, GenericResponseCodeSyntaxErrorFormat, "WEBALERT", token);

				Uri.TryCreate ((string) token.Value, UriKind.Absolute, out webalert.WebUri);

				token = ReadToken (cancellationToken);
				break;
			default:
				// Note: This code-path handles: [ALERT], [CLOSED], [READ-ONLY], [READ-WRITE], etc.

				//if (code.Type == ImapResponseCodeType.Unknown)
				//	Debug.WriteLine (string.Format ("Unknown RESP-CODE encountered: {0}", atom));

				// extensions are of the form: "[" atom [SPACE 1*<any TEXT_CHAR except "]">] "]"

				// skip over tokens until we get to a ']'
				while (token.Type != ImapTokenType.CloseBracket && token.Type != ImapTokenType.Eoln)
					token = ReadToken (cancellationToken);

				break;
			}

			AssertToken (token, ImapTokenType.CloseBracket, "Syntax error in response code. {0}", token);

			code.Message = ReadLine (cancellationToken).Trim ();

			return code;
		}

		/// <summary>
		/// Parses the response code.
		/// </summary>
		/// <returns>The response code.</returns>
		/// <param name="isTagged">Whether or not the resp-code is tagged vs untagged.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		public async ValueTask<ImapResponseCode> ParseResponseCodeAsync (bool isTagged, CancellationToken cancellationToken)
		{
			uint validity = Selected != null ? Selected.UidValidity : 0;
			ImapResponseCode code;
			string atom, value;
			ImapToken token;

			//			token = await ReadTokenAsync (cancellationToken).ConfigureAwait (false);
			//
			//			if (token.Type != ImapTokenType.LeftBracket) {
			//				Debug.WriteLine ("Expected a '[' followed by a RESP-CODE, but got: {0}", token);
			//				throw UnexpectedToken (token, false);
			//			}

			token = await ReadTokenAsync (cancellationToken).ConfigureAwait (false);

			AssertToken (token, ImapTokenType.Atom, "Syntax error in response code. {0}", token);

			atom = (string) token.Value;
			token = await ReadTokenAsync (cancellationToken).ConfigureAwait (false);

			code = ImapResponseCode.Create (GetResponseCodeType (atom));
			code.IsTagged = isTagged;

			switch (code.Type) {
			case ImapResponseCodeType.BadCharset:
				if (token.Type == ImapTokenType.OpenParen) {
					token = await ReadTokenAsync (cancellationToken).ConfigureAwait (false);

					SupportedCharsets.Clear ();
					while (token.Type == ImapTokenType.Atom || token.Type == ImapTokenType.QString) {
						SupportedCharsets.Add ((string) token.Value);
						token = await ReadTokenAsync (cancellationToken).ConfigureAwait (false);
					}

					AssertToken (token, ImapTokenType.CloseParen, GenericResponseCodeSyntaxErrorFormat, "BADCHARSET", token);

					token = await ReadTokenAsync (cancellationToken).ConfigureAwait (false);
				}
				break;
			case ImapResponseCodeType.Capability:
				Stream.UngetToken (token);
				await UpdateCapabilitiesAsync (ImapTokenType.CloseBracket, cancellationToken).ConfigureAwait (false);
				token = await ReadTokenAsync (cancellationToken).ConfigureAwait (false);
				break;
			case ImapResponseCodeType.PermanentFlags:
				var perm = (PermanentFlagsResponseCode) code;

				Stream.UngetToken (token);
				perm.Flags = await ImapUtils.ParseFlagsListAsync (this, "PERMANENTFLAGS", perm.Keywords, cancellationToken).ConfigureAwait (false);
				token = await ReadTokenAsync (cancellationToken).ConfigureAwait (false);
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
				token = await ReadTokenAsync (cancellationToken).ConfigureAwait (false);
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
				token = await ReadTokenAsync (cancellationToken).ConfigureAwait (false);
				break;
			case ImapResponseCodeType.Unseen:
				var unseen = (UnseenResponseCode) code;

				// Note: we allow '0' here because some servers have been known to send "* OK [UNSEEN 0]" when the
				// mailbox contains no messages.
				//
				// See https://github.com/jstedfast/MailKit/issues/34 for details.
				var n = ParseNumber (token, false, GenericResponseCodeSyntaxErrorFormat, "UNSEEN", token);

				unseen.Index = n > 0 ? (int) (n - 1) : 0;

				token = await ReadTokenAsync (cancellationToken).ConfigureAwait (false);
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
				token = await ReadTokenAsync (cancellationToken).ConfigureAwait (false);

				AssertToken (token, ImapTokenType.Atom, ImapTokenType.QString, GenericResponseCodeSyntaxErrorFormat, "NEWNAME", token);

				rename.NewName = (string) token.Value;

				token = await ReadTokenAsync (cancellationToken).ConfigureAwait (false);
				break;
			case ImapResponseCodeType.AppendUid:
				var append = (AppendUidResponseCode) code;

				append.UidValidity = ParseNumber (token, false, GenericResponseCodeSyntaxErrorFormat, "APPENDUID", token);

				token = await ReadTokenAsync (cancellationToken).ConfigureAwait (false);

				// The MULTIAPPEND extension redefines APPENDUID's second argument to be a uid-set instead of a single uid.
				append.UidSet = ParseUidSet (token, append.UidValidity, out _, out _, GenericResponseCodeSyntaxErrorFormat, "APPENDUID", token);

				token = await ReadTokenAsync (cancellationToken).ConfigureAwait (false);
				break;
			case ImapResponseCodeType.CopyUid:
				var copy = (CopyUidResponseCode) code;

				copy.UidValidity = ParseNumber (token, false, GenericResponseCodeSyntaxErrorFormat, "COPYUID", token);

				token = await ReadTokenAsync (cancellationToken).ConfigureAwait (false);

				// Note: Outlook.com will apparently sometimes issue a [COPYUID nz_number SPACE SPACE] resp-code
				// in response to a UID COPY or UID MOVE command. Likely this happens only when the source message
				// didn't exist or something? See https://github.com/jstedfast/MailKit/issues/555 for details.

				if (token.Type != ImapTokenType.CloseBracket) {
					copy.SrcUidSet = ParseUidSet (token, validity, out _, out _, GenericResponseCodeSyntaxErrorFormat, "COPYUID", token);
				} else {
					copy.SrcUidSet = new UniqueIdSet ();
					Stream.UngetToken (token);
				}

				token = await ReadTokenAsync (cancellationToken).ConfigureAwait (false);

				if (token.Type != ImapTokenType.CloseBracket) {
					copy.DestUidSet = ParseUidSet (token, copy.UidValidity, out _, out _, GenericResponseCodeSyntaxErrorFormat, "COPYUID", token);
				} else {
					copy.DestUidSet = new UniqueIdSet ();
					Stream.UngetToken (token);
				}

				token = await ReadTokenAsync (cancellationToken).ConfigureAwait (false);
				break;
			case ImapResponseCodeType.BadUrl:
				var badurl = (BadUrlResponseCode) code;

				AssertToken (token, ImapTokenType.Atom, ImapTokenType.QString, GenericResponseCodeSyntaxErrorFormat, "BADURL", token);

				badurl.BadUrl = (string) token.Value;

				token = await ReadTokenAsync (cancellationToken).ConfigureAwait (false);
				break;
			case ImapResponseCodeType.HighestModSeq:
				var highest = (HighestModSeqResponseCode) code;

				highest.HighestModSeq = ParseNumber64 (token, false, GenericResponseCodeSyntaxErrorFormat, "HIGHESTMODSEQ", token);

				token = await ReadTokenAsync (cancellationToken).ConfigureAwait (false);
				break;
			case ImapResponseCodeType.Modified:
				var modified = (ModifiedResponseCode) code;

				modified.UidSet = ParseUidSet (token, validity, out _, out _, GenericResponseCodeSyntaxErrorFormat, "MODIFIED", token);

				token = await ReadTokenAsync (cancellationToken).ConfigureAwait (false);
				break;
			case ImapResponseCodeType.MaxConvertMessages:
			case ImapResponseCodeType.MaxConvertParts:
				var maxConvert = (MaxConvertResponseCode) code;

				maxConvert.MaxConvert = ParseNumber (token, false, GenericResponseCodeSyntaxErrorFormat, atom, token);

				token = await ReadTokenAsync (cancellationToken).ConfigureAwait (false);
				break;
			case ImapResponseCodeType.NoUpdate:
				var noUpdate = (NoUpdateResponseCode) code;

				AssertToken (token, ImapTokenType.Atom, ImapTokenType.QString, GenericResponseCodeSyntaxErrorFormat, "NOUPDATE", token);

				noUpdate.Tag = (string) token.Value;

				token = await ReadTokenAsync (cancellationToken).ConfigureAwait (false);
				break;
			case ImapResponseCodeType.Annotate:
				var annotate = (AnnotateResponseCode) code;

				AssertToken (token, ImapTokenType.Atom, GenericResponseCodeSyntaxErrorFormat, "ANNOTATE", token);

				value = (string) token.Value;
				if (value.Equals ("TOOBIG", StringComparison.OrdinalIgnoreCase))
					annotate.SubType = AnnotateResponseCodeSubType.TooBig;
				else if (value.Equals ("TOOMANY", StringComparison.OrdinalIgnoreCase))
					annotate.SubType = AnnotateResponseCodeSubType.TooMany;

				token = await ReadTokenAsync (cancellationToken).ConfigureAwait (false);
				break;
			case ImapResponseCodeType.Annotations:
				var annotations = (AnnotationsResponseCode) code;

				AssertToken (token, ImapTokenType.Atom, GenericResponseCodeSyntaxErrorFormat, "ANNOTATIONS", token);

				value = (string) token.Value;
				if (value.Equals ("NONE", StringComparison.OrdinalIgnoreCase)) {
					// nothing
				} else if (value.Equals ("READ-ONLY", StringComparison.OrdinalIgnoreCase)) {
					annotations.Access = AnnotationAccess.ReadOnly;
				} else {
					annotations.Access = AnnotationAccess.ReadWrite;
					annotations.MaxSize = ParseNumber (token, false, GenericResponseCodeSyntaxErrorFormat, "ANNOTATIONS", token);
				}

				token = await ReadTokenAsync (cancellationToken).ConfigureAwait (false);

				if (annotations.Access != AnnotationAccess.None) {
					annotations.Scopes = AnnotationScope.Both;

					if (token.Type != ImapTokenType.CloseBracket) {
						AssertToken (token, ImapTokenType.Atom, GenericResponseCodeSyntaxErrorFormat, "ANNOTATIONS", token);

						if (((string) token.Value).Equals ("NOPRIVATE", StringComparison.OrdinalIgnoreCase))
							annotations.Scopes = AnnotationScope.Shared;

						token = await ReadTokenAsync (cancellationToken).ConfigureAwait (false);
					}
				}

				break;
			case ImapResponseCodeType.Metadata:
				var metadata = (MetadataResponseCode) code;

				AssertToken (token, ImapTokenType.Atom, GenericResponseCodeSyntaxErrorFormat, "METADATA", token);

				value = (string) token.Value;
				if (value.Equals ("LONGENTRIES", StringComparison.OrdinalIgnoreCase)) {
					metadata.SubType = MetadataResponseCodeSubType.LongEntries;
					metadata.IsError = false;

					token = await ReadTokenAsync (cancellationToken).ConfigureAwait (false);

					metadata.Value = ParseNumber (token, false, GenericResponseCodeSyntaxErrorFormat, "METADATA LONGENTRIES", token);
				} else if (value.Equals ("MAXSIZE", StringComparison.OrdinalIgnoreCase)) {
					metadata.SubType = MetadataResponseCodeSubType.MaxSize;

					token = await ReadTokenAsync (cancellationToken).ConfigureAwait (false);

					metadata.Value = ParseNumber (token, false, GenericResponseCodeSyntaxErrorFormat, "METADATA MAXSIZE", token);
				} else if (value.Equals ("TOOMANY", StringComparison.OrdinalIgnoreCase)) {
					metadata.SubType = MetadataResponseCodeSubType.TooMany;
				} else if (value.Equals ("NOPRIVATE", StringComparison.OrdinalIgnoreCase)) {
					metadata.SubType = MetadataResponseCodeSubType.NoPrivate;
				}

				token = await ReadTokenAsync (cancellationToken).ConfigureAwait (false);
				break;
			case ImapResponseCodeType.UndefinedFilter:
				var undefined = (UndefinedFilterResponseCode) code;

				AssertToken (token, ImapTokenType.Atom, GenericResponseCodeSyntaxErrorFormat, "UNDEFINED-FILTER", token);

				undefined.Name = (string) token.Value;

				token = await ReadTokenAsync (cancellationToken).ConfigureAwait (false);
				break;
			case ImapResponseCodeType.MailboxId:
				var mailboxid = (MailboxIdResponseCode) code;

				AssertToken (token, ImapTokenType.OpenParen, GenericResponseCodeSyntaxErrorFormat, "MAILBOXID", token);

				token = await ReadTokenAsync (cancellationToken).ConfigureAwait (false);

				AssertToken (token, ImapTokenType.Atom, GenericResponseCodeSyntaxErrorFormat, "MAILBOXID", token);

				mailboxid.MailboxId = (string) token.Value;

				token = await ReadTokenAsync (cancellationToken).ConfigureAwait (false);

				AssertToken (token, ImapTokenType.CloseParen, GenericResponseCodeSyntaxErrorFormat, "MAILBOXID", token);

				token = await ReadTokenAsync (cancellationToken).ConfigureAwait (false);
				break;
			case ImapResponseCodeType.WebAlert:
				var webalert = (WebAlertResponseCode) code;

				AssertToken (token, ImapTokenType.Atom, GenericResponseCodeSyntaxErrorFormat, "WEBALERT", token);

				Uri.TryCreate ((string) token.Value, UriKind.Absolute, out webalert.WebUri);

				token = await ReadTokenAsync (cancellationToken).ConfigureAwait (false);
				break;
			default:
				// Note: This code-path handles: [ALERT], [CLOSED], [READ-ONLY], [READ-WRITE], etc.

				//if (code.Type == ImapResponseCodeType.Unknown)
				//	Debug.WriteLine (string.Format ("Unknown RESP-CODE encountered: {0}", atom));

				// extensions are of the form: "[" atom [SPACE 1*<any TEXT_CHAR except "]">] "]"

				// skip over tokens until we get to a ']'
				while (token.Type != ImapTokenType.CloseBracket && token.Type != ImapTokenType.Eoln)
					token = await ReadTokenAsync (cancellationToken).ConfigureAwait (false);

				break;
			}

			AssertToken (token, ImapTokenType.CloseBracket, "Syntax error in response code. {0}", token);

			code.Message = (await ReadLineAsync (cancellationToken).ConfigureAwait (false)).Trim ();

			return code;
		}

		bool UpdateSimpleStatusValue (ImapFolder folder, string atom, ImapToken token)
		{
			uint count, uid;
			ulong modseq;

			if (atom.Equals ("HIGHESTMODSEQ", StringComparison.OrdinalIgnoreCase)) {
				AssertToken (token, ImapTokenType.Atom, GenericUntaggedResponseSyntaxErrorFormat, "STATUS", token);

				modseq = ParseNumber64 (token, false, GenericItemSyntaxErrorFormat, atom, token);

				folder?.UpdateHighestModSeq (modseq);
			} else if (atom.Equals ("MESSAGES", StringComparison.OrdinalIgnoreCase)) {
				AssertToken (token, ImapTokenType.Atom, GenericUntaggedResponseSyntaxErrorFormat, "STATUS", token);

				count = ParseNumber (token, false, GenericItemSyntaxErrorFormat, atom, token);

				folder?.OnExists ((int) count);
			} else if (atom.Equals ("RECENT", StringComparison.OrdinalIgnoreCase)) {
				AssertToken (token, ImapTokenType.Atom, GenericUntaggedResponseSyntaxErrorFormat, "STATUS", token);

				count = ParseNumber (token, false, GenericItemSyntaxErrorFormat, atom, token);

				folder?.OnRecent ((int) count);
			} else if (atom.Equals ("UIDNEXT", StringComparison.OrdinalIgnoreCase)) {
				AssertToken (token, ImapTokenType.Atom, GenericUntaggedResponseSyntaxErrorFormat, "STATUS", token);

				uid = ParseNumber (token, false, GenericItemSyntaxErrorFormat, atom, token);

				folder?.UpdateUidNext (uid > 0 ? new UniqueId (uid) : UniqueId.Invalid);
			} else if (atom.Equals ("UIDVALIDITY", StringComparison.OrdinalIgnoreCase)) {
				AssertToken (token, ImapTokenType.Atom, GenericUntaggedResponseSyntaxErrorFormat, "STATUS", token);

				uid = ParseNumber (token, false, GenericItemSyntaxErrorFormat, atom, token);

				folder?.UpdateUidValidity (uid);
			} else if (atom.Equals ("UNSEEN", StringComparison.OrdinalIgnoreCase)) {
				AssertToken (token, ImapTokenType.Atom, GenericUntaggedResponseSyntaxErrorFormat, "STATUS", token);

				count = ParseNumber (token, false, GenericItemSyntaxErrorFormat, atom, token);

				folder?.UpdateUnread ((int) count);
			} else if (atom.Equals ("APPENDLIMIT", StringComparison.OrdinalIgnoreCase)) {
				if (token.Type == ImapTokenType.Atom) {
					var limit = ParseNumber (token, false, GenericItemSyntaxErrorFormat, atom, token);

					folder?.UpdateAppendLimit (limit);
				} else {
					AssertToken (token, ImapTokenType.Nil, GenericUntaggedResponseSyntaxErrorFormat, "STATUS", token);

					folder?.UpdateAppendLimit (null);
				}
			} else if (atom.Equals ("SIZE", StringComparison.OrdinalIgnoreCase)) {
				AssertToken (token, ImapTokenType.Atom, GenericUntaggedResponseSyntaxErrorFormat, "STATUS", token);

				var size = ParseNumber64 (token, false, GenericItemSyntaxErrorFormat, atom, token);

				folder?.UpdateSize (size);
			} else {
				// This is probably the MAILBOXID value which is multiple tokens and can't be handled here.
				return false;
			}

			return true;
		}

		void UpdateStatus (CancellationToken cancellationToken)
		{
			var token = ReadToken (ImapStream.AtomSpecials, cancellationToken);
			string name;

			switch (token.Type) {
			case ImapTokenType.Literal:
				name = ReadLiteral (cancellationToken);
				break;
			case ImapTokenType.QString:
			case ImapTokenType.Atom:
				name = (string) token.Value;
				break;
			case ImapTokenType.Nil:
				// Note: according to rfc3501, section 4.5, NIL is acceptable as a mailbox name.
				name = (string) token.Value;
				break;
			default:
				throw UnexpectedToken (GenericUntaggedResponseSyntaxErrorFormat, "STATUS", token);
			}

			// Note: if the folder is null, then it probably means the user is using NOTIFY
			// and hasn't yet requested the folder. That's ok.
			TryGetCachedFolder (name, out var folder);

			token = ReadToken (cancellationToken);

			AssertToken (token, ImapTokenType.OpenParen, GenericUntaggedResponseSyntaxErrorFormat, "STATUS", token);

			do {
				token = ReadToken (cancellationToken);

				if (token.Type == ImapTokenType.CloseParen)
					break;

				AssertToken (token, ImapTokenType.Atom, GenericUntaggedResponseSyntaxErrorFormat, "STATUS", token);

				var atom = (string) token.Value;

				token = ReadToken (cancellationToken);

				if (UpdateSimpleStatusValue (folder, atom, token))
					continue;

				if (atom.Equals ("MAILBOXID", StringComparison.OrdinalIgnoreCase)) {
					AssertToken (token, ImapTokenType.OpenParen, GenericUntaggedResponseSyntaxErrorFormat, "STATUS", token);

					token = ReadToken (cancellationToken);

					AssertToken (token, ImapTokenType.Atom, GenericItemSyntaxErrorFormat, atom, token);

					folder?.UpdateId ((string) token.Value);

					token = ReadToken (cancellationToken);

					AssertToken (token, ImapTokenType.CloseParen, GenericUntaggedResponseSyntaxErrorFormat, "STATUS", token);
				}
			} while (true);

			token = ReadToken (cancellationToken);

			AssertToken (token, ImapTokenType.Eoln, GenericUntaggedResponseSyntaxErrorFormat, "STATUS", token);
		}

		async ValueTask UpdateStatusAsync (CancellationToken cancellationToken)
		{
			var token = await ReadTokenAsync (ImapStream.AtomSpecials, cancellationToken).ConfigureAwait (false);
			string name;

			switch (token.Type) {
			case ImapTokenType.Literal:
				name = await ReadLiteralAsync (cancellationToken).ConfigureAwait (false);
				break;
			case ImapTokenType.QString:
			case ImapTokenType.Atom:
				name = (string) token.Value;
				break;
			case ImapTokenType.Nil:
				// Note: according to rfc3501, section 4.5, NIL is acceptable as a mailbox name.
				name = (string) token.Value;
				break;
			default:
				throw UnexpectedToken (GenericUntaggedResponseSyntaxErrorFormat, "STATUS", token);
			}

			// Note: if the folder is null, then it probably means the user is using NOTIFY
			// and hasn't yet requested the folder. That's ok.
			TryGetCachedFolder (name, out var folder);

			token = await ReadTokenAsync (cancellationToken).ConfigureAwait (false);

			AssertToken (token, ImapTokenType.OpenParen, GenericUntaggedResponseSyntaxErrorFormat, "STATUS", token);

			do {
				token = await ReadTokenAsync (cancellationToken).ConfigureAwait (false);

				if (token.Type == ImapTokenType.CloseParen)
					break;

				AssertToken (token, ImapTokenType.Atom, GenericUntaggedResponseSyntaxErrorFormat, "STATUS", token);

				var atom = (string) token.Value;

				token = await ReadTokenAsync (cancellationToken).ConfigureAwait (false);

				if (UpdateSimpleStatusValue (folder, atom, token))
					continue;

				if (atom.Equals ("MAILBOXID", StringComparison.OrdinalIgnoreCase)) {
					AssertToken (token, ImapTokenType.OpenParen, GenericUntaggedResponseSyntaxErrorFormat, "STATUS", token);

					token = await ReadTokenAsync (cancellationToken).ConfigureAwait (false);

					AssertToken (token, ImapTokenType.Atom, GenericItemSyntaxErrorFormat, atom, token);

					folder?.UpdateId ((string) token.Value);

					token = await ReadTokenAsync (cancellationToken).ConfigureAwait (false);

					AssertToken (token, ImapTokenType.CloseParen, GenericUntaggedResponseSyntaxErrorFormat, "STATUS", token);
				}
			} while (true);

			token = await ReadTokenAsync (cancellationToken).ConfigureAwait (false);

			AssertToken (token, ImapTokenType.Eoln, GenericUntaggedResponseSyntaxErrorFormat, "STATUS", token);
		}

		static bool IsOkNoOrBad (string atom, out ImapUntaggedResult result)
		{
			if (atom.Equals ("OK", StringComparison.OrdinalIgnoreCase)) {
				result = ImapUntaggedResult.Ok;
				return true;
			}
			
			if (atom.Equals ("NO", StringComparison.OrdinalIgnoreCase)) {
				result = ImapUntaggedResult.No;
				return true;
			}

			if (atom.Equals ("BAD", StringComparison.OrdinalIgnoreCase)) {
				result = ImapUntaggedResult.Bad;
				return true;
			}

			result = ImapUntaggedResult.Ok;

			return false;
		}

		/// <summary>
		/// Processes an untagged response.
		/// </summary>
		/// <returns>The untagged response.</returns>
		/// <param name="cancellationToken">The cancellation token.</param>
		internal void ProcessUntaggedResponse (CancellationToken cancellationToken)
		{
			var token = ReadToken (cancellationToken);
			var folder = current.Folder ?? Selected;
			ImapUntaggedHandler handler;
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
				SkipLine (cancellationToken);
				return;
			} else {
				atom = (string) token.Value;
			}

			if (atom.Equals ("BYE", StringComparison.OrdinalIgnoreCase)) {
				token = ReadToken (cancellationToken);

				if (token.Type == ImapTokenType.OpenBracket) {
					var code = ParseResponseCode (false, cancellationToken);
					current.RespCodes.Add (code);
				} else {
					var text = ReadLine (cancellationToken).TrimEnd ();
					current.ResponseText = token.Value.ToString () + text;
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
			} else if (atom.Equals ("CAPABILITY", StringComparison.OrdinalIgnoreCase)) {
				UpdateCapabilities (ImapTokenType.Eoln, cancellationToken);

				// read the eoln token
				ReadToken (cancellationToken);
			} else if (atom.Equals ("ENABLED", StringComparison.OrdinalIgnoreCase)) {
				do {
					token = ReadToken (cancellationToken);

					if (token.Type == ImapTokenType.Eoln)
						break;

					AssertToken (token, ImapTokenType.Atom, GenericUntaggedResponseSyntaxErrorFormat, atom, token);

					var feature = (string) token.Value;
					if (feature.Equals ("UTF8=ACCEPT", StringComparison.OrdinalIgnoreCase))
						UTF8Enabled = true;
					else if (feature.Equals ("QRESYNC", StringComparison.OrdinalIgnoreCase))
						QResyncEnabled = true;
				} while (true);
			} else if (atom.Equals ("FLAGS", StringComparison.OrdinalIgnoreCase)) {
				var keywords = new HashSet<string> (StringComparer.Ordinal);
				var flags = ImapUtils.ParseFlagsList (this, atom, keywords, cancellationToken);
				folder.UpdateAcceptedFlags (flags, keywords);
				token = ReadToken (cancellationToken);

				AssertToken (token, ImapTokenType.Eoln, GenericUntaggedResponseSyntaxErrorFormat, atom, token);
			} else if (atom.Equals ("NAMESPACE", StringComparison.OrdinalIgnoreCase)) {
				UpdateNamespaces (cancellationToken);
			} else if (atom.Equals ("STATUS", StringComparison.OrdinalIgnoreCase)) {
				UpdateStatus (cancellationToken);
			} else if (IsOkNoOrBad (atom, out var result)) {
				token = ReadToken (cancellationToken);

				if (token.Type == ImapTokenType.OpenBracket) {
					var code = ParseResponseCode (false, cancellationToken);
					current.RespCodes.Add (code);
				} else if (token.Type != ImapTokenType.Eoln) {
					var text = ReadLine (cancellationToken).TrimEnd ();
					current.ResponseText = token.Value.ToString () + text;
				}
			} else {
				if (uint.TryParse (atom, NumberStyles.None, CultureInfo.InvariantCulture, out uint number)) {
					// we probably have something like "* 1 EXISTS"
					token = ReadToken (cancellationToken);

					AssertToken (token, ImapTokenType.Atom, "Syntax error in untagged response. {0}", token);

					atom = (string) token.Value;

					if (current.UntaggedHandlers.TryGetValue (atom, out handler)) {
						// the command registered an untagged handler for this atom...
						handler (this, current, (int) number - 1, false).GetAwaiter ().GetResult ();
					} else if (folder != null) {
						if (atom.Equals ("EXISTS", StringComparison.OrdinalIgnoreCase)) {
							folder.OnExists ((int) number);
						} else if (atom.Equals ("EXPUNGE", StringComparison.OrdinalIgnoreCase)) {
							if (number == 0)
								throw UnexpectedToken ("Syntax error in untagged EXPUNGE response. Unexpected message index: 0");

							folder.OnExpunge ((int) number - 1);
						} else if (atom.Equals ("FETCH", StringComparison.OrdinalIgnoreCase)) {
							// Apparently Courier-IMAP (2004) will reply with "* 0 FETCH ..." sometimes.
							// See https://github.com/jstedfast/MailKit/issues/428 for details.
							//if (number == 0)
							//	throw UnexpectedToken ("Syntax error in untagged FETCH response. Unexpected message index: 0");

							folder.OnUntaggedFetchResponse (this, (int) number - 1, cancellationToken);
						} else if (atom.Equals ("RECENT", StringComparison.OrdinalIgnoreCase)) {
							folder.OnRecent ((int) number);
						} else {
							//Debug.WriteLine ("Unhandled untagged response: * {0} {1}", number, atom);
						}
					} else {
						//Debug.WriteLine ("Unhandled untagged response: * {0} {1}", number, atom);
					}

					SkipLine (cancellationToken);
				} else if (current.UntaggedHandlers.TryGetValue (atom, out handler)) {
					// the command registered an untagged handler for this atom...
					handler (this, current, -1, false).GetAwaiter ().GetResult ();
					SkipLine (cancellationToken);
				} else if (atom.Equals ("LIST", StringComparison.OrdinalIgnoreCase)) {
					// unsolicited LIST response - probably due to NOTIFY MailboxName or MailboxSubscribe event
					ImapUtils.ParseFolderList (this, null, false, true, cancellationToken);
					token = ReadToken (cancellationToken);
					AssertToken (token, ImapTokenType.Eoln, "Syntax error in untagged LIST response. {0}", token);
				} else if (atom.Equals ("METADATA", StringComparison.OrdinalIgnoreCase)) {
					// unsolicited METADATA response - probably due to NOTIFY MailboxMetadataChange or ServerMetadataChange
					var metadata = new MetadataCollection ();
					ImapUtils.ParseMetadata (this, metadata, cancellationToken);
					ProcessMetadataChanges (metadata);

					token = ReadToken (cancellationToken);
					AssertToken (token, ImapTokenType.Eoln, "Syntax error in untagged LIST response. {0}", token);
				} else if (atom.Equals ("VANISHED", StringComparison.OrdinalIgnoreCase) && folder != null) {
					folder.OnVanished (this, cancellationToken);
					SkipLine (cancellationToken);
				} else {
					// don't know how to handle this... eat it?
					SkipLine (cancellationToken);
				}
			}
		}

		/// <summary>
		/// Processes an untagged response.
		/// </summary>
		/// <returns>The untagged response.</returns>
		/// <param name="cancellationToken">The cancellation token.</param>
		internal async Task ProcessUntaggedResponseAsync (CancellationToken cancellationToken)
		{
			var token = await ReadTokenAsync (cancellationToken).ConfigureAwait (false);
			var folder = current.Folder ?? Selected;
			ImapUntaggedHandler handler;
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
				await SkipLineAsync (cancellationToken).ConfigureAwait (false);
				return;
			} else {
				atom = (string) token.Value;
			}

			if (atom.Equals ("BYE", StringComparison.OrdinalIgnoreCase)) {
				token = await ReadTokenAsync (cancellationToken).ConfigureAwait (false);

				if (token.Type == ImapTokenType.OpenBracket) {
					var code = await ParseResponseCodeAsync (false, cancellationToken).ConfigureAwait (false);
					current.RespCodes.Add (code);
				} else {
					var text = (await ReadLineAsync (cancellationToken).ConfigureAwait (false)).TrimEnd ();
					current.ResponseText = token.Value.ToString () + text;
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
			} else if (atom.Equals ("CAPABILITY", StringComparison.OrdinalIgnoreCase)) {
				await UpdateCapabilitiesAsync (ImapTokenType.Eoln, cancellationToken).ConfigureAwait (false);

				// read the eoln token
				await ReadTokenAsync (cancellationToken).ConfigureAwait (false);
			} else if (atom.Equals ("ENABLED", StringComparison.OrdinalIgnoreCase)) {
				do {
					token = await ReadTokenAsync (cancellationToken).ConfigureAwait (false);

					if (token.Type == ImapTokenType.Eoln)
						break;

					AssertToken (token, ImapTokenType.Atom, GenericUntaggedResponseSyntaxErrorFormat, atom, token);

					var feature = (string) token.Value;
					if (feature.Equals ("UTF8=ACCEPT", StringComparison.OrdinalIgnoreCase))
						UTF8Enabled = true;
					else if (feature.Equals ("QRESYNC", StringComparison.OrdinalIgnoreCase))
						QResyncEnabled = true;
				} while (true);
			} else if (atom.Equals ("FLAGS", StringComparison.OrdinalIgnoreCase)) {
				var keywords = new HashSet<string> (StringComparer.Ordinal);
				var flags = await ImapUtils.ParseFlagsListAsync (this, atom, keywords, cancellationToken).ConfigureAwait (false);
				folder.UpdateAcceptedFlags (flags, keywords);
				token = await ReadTokenAsync (cancellationToken).ConfigureAwait (false);

				AssertToken (token, ImapTokenType.Eoln, GenericUntaggedResponseSyntaxErrorFormat, atom, token);
			} else if (atom.Equals ("NAMESPACE", StringComparison.OrdinalIgnoreCase)) {
				await UpdateNamespacesAsync (cancellationToken).ConfigureAwait (false);
			} else if (atom.Equals ("STATUS", StringComparison.OrdinalIgnoreCase)) {
				await UpdateStatusAsync (cancellationToken).ConfigureAwait (false);
			} else if (IsOkNoOrBad (atom, out var result)) {
				token = await ReadTokenAsync (cancellationToken).ConfigureAwait (false);

				if (token.Type == ImapTokenType.OpenBracket) {
					var code = await ParseResponseCodeAsync (false, cancellationToken).ConfigureAwait (false);
					current.RespCodes.Add (code);
				} else if (token.Type != ImapTokenType.Eoln) {
					var text = (await ReadLineAsync (cancellationToken).ConfigureAwait (false)).TrimEnd ();
					current.ResponseText = token.Value.ToString () + text;
				}
			} else {
				if (uint.TryParse (atom, NumberStyles.None, CultureInfo.InvariantCulture, out uint number)) {
					// we probably have something like "* 1 EXISTS"
					token = await ReadTokenAsync (cancellationToken).ConfigureAwait (false);

					AssertToken (token, ImapTokenType.Atom, "Syntax error in untagged response. {0}", token);

					atom = (string) token.Value;

					if (current.UntaggedHandlers.TryGetValue (atom, out handler)) {
						// the command registered an untagged handler for this atom...
						await handler (this, current, (int) number - 1, doAsync: true).ConfigureAwait (false);
					} else if (folder != null) {
						if (atom.Equals ("EXISTS", StringComparison.OrdinalIgnoreCase)) {
							folder.OnExists ((int) number);
						} else if (atom.Equals ("EXPUNGE", StringComparison.OrdinalIgnoreCase)) {
							if (number == 0)
								throw UnexpectedToken ("Syntax error in untagged EXPUNGE response. Unexpected message index: 0");

							folder.OnExpunge ((int) number - 1);
						} else if (atom.Equals ("FETCH", StringComparison.OrdinalIgnoreCase)) {
							// Apparently Courier-IMAP (2004) will reply with "* 0 FETCH ..." sometimes.
							// See https://github.com/jstedfast/MailKit/issues/428 for details.
							//if (number == 0)
							//	throw UnexpectedToken ("Syntax error in untagged FETCH response. Unexpected message index: 0");

							await folder.OnUntaggedFetchResponseAsync (this, (int) number - 1, cancellationToken).ConfigureAwait (false);
						} else if (atom.Equals ("RECENT", StringComparison.OrdinalIgnoreCase)) {
							folder.OnRecent ((int) number);
						} else {
							//Debug.WriteLine ("Unhandled untagged response: * {0} {1}", number, atom);
						}
					} else {
						//Debug.WriteLine ("Unhandled untagged response: * {0} {1}", number, atom);
					}

					await SkipLineAsync (cancellationToken).ConfigureAwait (false);
				} else if (current.UntaggedHandlers.TryGetValue (atom, out handler)) {
					// the command registered an untagged handler for this atom...
					await handler (this, current, -1, doAsync: true).ConfigureAwait (false);
					await SkipLineAsync (cancellationToken).ConfigureAwait (false);
				} else if (atom.Equals ("LIST", StringComparison.OrdinalIgnoreCase)) {
					// unsolicited LIST response - probably due to NOTIFY MailboxName or MailboxSubscribe event
					await ImapUtils.ParseFolderListAsync (this, null, false, true, cancellationToken).ConfigureAwait (false);
					token = await ReadTokenAsync (cancellationToken).ConfigureAwait (false);
					AssertToken (token, ImapTokenType.Eoln, "Syntax error in untagged LIST response. {0}", token);
				} else if (atom.Equals ("METADATA", StringComparison.OrdinalIgnoreCase)) {
					// unsolicited METADATA response - probably due to NOTIFY MailboxMetadataChange or ServerMetadataChange
					var metadata = new MetadataCollection ();
					await ImapUtils.ParseMetadataAsync (this, metadata, cancellationToken).ConfigureAwait (false);
					ProcessMetadataChanges (metadata);

					token = await ReadTokenAsync (cancellationToken).ConfigureAwait (false);
					AssertToken (token, ImapTokenType.Eoln, "Syntax error in untagged LIST response. {0}", token);
				} else if (atom.Equals ("VANISHED", StringComparison.OrdinalIgnoreCase) && folder != null) {
					await folder.OnVanishedAsync (this, cancellationToken).ConfigureAwait (false);
					await SkipLineAsync (cancellationToken).ConfigureAwait (false);
				} else {
					// don't know how to handle this... eat it?
					await SkipLineAsync (cancellationToken).ConfigureAwait (false);
				}
			}
		}

		void PopNextCommand ()
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
		}

		void OnImapProtocolException (ImapProtocolException ex)
		{
			var ic = current;

			Disconnect (ex);

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
		}

		/// <summary>
		/// Iterate the command pipeline.
		/// </summary>
		void Iterate ()
		{
			PopNextCommand ();

			current.Status = ImapCommandStatus.Active;

			try {
				while (current.Step ()) {
					// more literal data to send...
				}

				if (current.Bye && !current.Logout)
					throw new ImapProtocolException ("Bye.");
			} catch (ImapProtocolException ex) {
				OnImapProtocolException (ex);
				throw;
			} catch (Exception ex) {
				Disconnect (ex);
				throw;
			} finally {
				current = null;
			}
		}

		/// <summary>
		/// Asynchronously iterate the command pipeline.
		/// </summary>
		async Task IterateAsync ()
		{
			PopNextCommand ();

			current.Status = ImapCommandStatus.Active;

			try {
				while (await current.StepAsync ().ConfigureAwait (false)) {
					// more literal data to send...
				}

				if (current.Bye && !current.Logout)
					throw new ImapProtocolException ("Bye.");
			} catch (ImapProtocolException ex) {
				OnImapProtocolException (ex);
				throw;
			} catch (Exception ex) {
				Disconnect (ex);
				throw;
			} finally {
				current = null;
			}
		}

		/// <summary>
		/// Wait for the specified command to finish.
		/// </summary>
		/// <param name="ic">The IMAP command.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <paramref name="ic"/> is <c>null</c>.
		/// </exception>
		public ImapCommandResponse Run (ImapCommand ic)
		{
			if (ic == null)
				throw new ArgumentNullException (nameof (ic));

			while (ic.Status < ImapCommandStatus.Complete) {
				// continue processing commands...
				Iterate ();
			}

			ProcessResponseCodes (ic);

			return ic.Response;
		}

		/// <summary>
		/// Wait for the specified command to finish.
		/// </summary>
		/// <param name="ic">The IMAP command.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <paramref name="ic"/> is <c>null</c>.
		/// </exception>
		public async Task<ImapCommandResponse> RunAsync (ImapCommand ic)
		{
			if (ic == null)
				throw new ArgumentNullException (nameof (ic));

			while (ic.Status < ImapCommandStatus.Complete) {
				// continue processing commands...
				await IterateAsync ().ConfigureAwait (false);
			}

			ProcessResponseCodes (ic);

			return ic.Response;
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
		/// <param name="cancellationToken">The cancellation token.</param>
		public ImapCommandResponse QueryCapabilities (CancellationToken cancellationToken)
		{
			var ic = QueueCommand (cancellationToken, null, "CAPABILITY\r\n");

			return Run (ic);
		}

		/// <summary>
		/// Queries the capabilities.
		/// </summary>
		/// <returns>The command result.</returns>
		/// <param name="cancellationToken">The cancellation token.</param>
		public Task<ImapCommandResponse> QueryCapabilitiesAsync (CancellationToken cancellationToken)
		{
			var ic = QueueCommand (cancellationToken, null, "CAPABILITY\r\n");

			return RunAsync (ic);
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
		public bool TryGetCachedFolder (string encodedName, out ImapFolder folder)
		{
			return FolderCache.TryGetValue (encodedName, out folder);
		}

		bool RequiresParentLookup (ImapFolder folder, out string encodedParentName)
		{
			encodedParentName = null;

			if (folder.ParentFolder != null)
				return false;

			int index;

			// FIXME: should this search EncodedName instead of FullName?
			if ((index = folder.FullName.LastIndexOf (folder.DirectorySeparator)) != -1) {
				if (index == 0)
					return false;

				var parentName = folder.FullName.Substring (0, index);
				encodedParentName = EncodeMailboxName (parentName);
			} else {
				encodedParentName = string.Empty;
			}

			if (TryGetCachedFolder (encodedParentName, out var parent)) {
				folder.ParentFolder = parent;
				return false;
			}

			return true;
		}

		ImapCommand QueueLookupParentFolderCommand (string encodedName, CancellationToken cancellationToken)
		{
			// Note: folder names can contain wildcards (including '*' and '%'), so replace '*' with '%'
			// in order to reduce the list of folders returned by our LIST command.
			var pattern = encodedName.Replace ('*', '%');
			var command = new StringBuilder ("LIST \"\" %S");
			var returnsSubscribed = false;

			if ((Capabilities & ImapCapabilities.ListExtended) != 0) {
				// Try to get the \Subscribed and \HasChildren or \HasNoChildren attributes
				command.Append (" RETURN (SUBSCRIBED CHILDREN)");
				returnsSubscribed = true;
			}

			command.Append ("\r\n");

			var ic = new ImapCommand (this, cancellationToken, null, command.ToString (), pattern);
			ic.RegisterUntaggedHandler ("LIST", ImapUtils.UntaggedListHandler);
			ic.ListReturnsSubscribed = returnsSubscribed;
			ic.UserData = new List<ImapFolder> ();

			QueueCommand (ic);

			return ic;
		}

		void ProcessLookupParentFolderResponse (ImapCommand ic, List<ImapFolder> list, ImapFolder folder, string encodedParentName)
		{
			if (!TryGetCachedFolder (encodedParentName, out var parent)) {
				parent = CreateImapFolder (encodedParentName, FolderAttributes.NonExistent, folder.DirectorySeparator);
				CacheFolder (parent);
			} else if (parent.ParentFolder == null && !parent.IsNamespace) {
				list.Add (parent);
			}

			folder.ParentFolder = parent;
		}

		/// <summary>
		/// Looks up and sets the <see cref="MailFolder.ParentFolder"/> property of each of the folders.
		/// </summary>
		/// <param name="folders">The IMAP folders.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		internal void LookupParentFolders (IEnumerable<ImapFolder> folders, CancellationToken cancellationToken)
		{
			var list = new List<ImapFolder> (folders);

			// Note: we use a for-loop instead of foreach because we conditionally add items to the list.
			for (int i = 0; i < list.Count; i++) {
				var folder = list[i];

				if (!RequiresParentLookup (folder, out var encodedParentName))
					continue;

				var ic = QueueLookupParentFolderCommand (encodedParentName, cancellationToken);

				Run (ic);

				ProcessLookupParentFolderResponse (ic, list, folder, encodedParentName);
			}
		}

		/// <summary>
		/// Looks up and sets the <see cref="MailFolder.ParentFolder"/> property of each of the folders.
		/// </summary>
		/// <param name="folders">The IMAP folders.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		internal async Task LookupParentFoldersAsync (IEnumerable<ImapFolder> folders, CancellationToken cancellationToken)
		{
			var list = new List<ImapFolder> (folders);

			// Note: we use a for-loop instead of foreach because we conditionally add items to the list.
			for (int i = 0; i < list.Count; i++) {
				var folder = list[i];

				if (!RequiresParentLookup (folder, out var encodedParentName))
					continue;

				var ic = QueueLookupParentFolderCommand (encodedParentName, cancellationToken);

				await RunAsync (ic).ConfigureAwait (false);

				ProcessLookupParentFolderResponse (ic, list, folder, encodedParentName);
			}
		}

		void ProcessNamespaceResponse (ImapCommand ic)
		{
			if (QuirksMode == ImapQuirksMode.Exchange && ic.Response == ImapCommandResponse.Bad) {
				State = ImapEngineState.Connected; // Reset back to Connected-but-not-Authenticated state
				throw ImapCommandException.Create ("NAMESPACE", ic);
			}
		}

		ImapCommand QueueListNamespaceCommand (List<ImapFolder> list, CancellationToken cancellationToken)
		{
			var ic = new ImapCommand (this, cancellationToken, null, "LIST \"\" \"\"\r\n");
			ic.RegisterUntaggedHandler ("LIST", ImapUtils.UntaggedListHandler);
			ic.UserData = list;

			QueueCommand (ic);

			return ic;
		}

		void ProcessListNamespaceResponse (ImapCommand ic, List<ImapFolder> list)
		{
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
		}

		/// <summary>
		/// Queries the namespaces.
		/// </summary>
		/// <returns>The command result.</returns>
		/// <param name="cancellationToken">The cancellation token.</param>
		public ImapCommandResponse QueryNamespaces (CancellationToken cancellationToken)
		{
			ImapCommand ic;

			// Note: It seems that on Exchange 2003 (maybe Chinese-only version?), the NAMESPACE command causes the server
			// to immediately drop the connection. Avoid this issue by not using the NAMESPACE command if we detect that
			// the server is Microsoft Exchange 2003. See https://github.com/jstedfast/MailKit/issues/1512 for details.
			if (QuirksMode != ImapQuirksMode.Exchange2003 && (Capabilities & ImapCapabilities.Namespace) != 0) {
				ic = QueueCommand (cancellationToken, null, "NAMESPACE\r\n");

				Run (ic);

				ProcessNamespaceResponse (ic);
			} else {
				var list = new List<ImapFolder> ();

				ic = QueueListNamespaceCommand (list, cancellationToken);

				Run (ic);

				ProcessListNamespaceResponse (ic, list);

				LookupParentFolders (list, cancellationToken);
			}

			return ic.Response;
		}

		/// <summary>
		/// Asynchronously queries the namespaces.
		/// </summary>
		/// <returns>The command result.</returns>
		/// <param name="cancellationToken">The cancellation token.</param>
		public async Task<ImapCommandResponse> QueryNamespacesAsync (CancellationToken cancellationToken)
		{
			ImapCommand ic;

			// Note: It seems that on Exchange 2003 (maybe Chinese-only version?), the NAMESPACE command causes the server
			// to immediately drop the connection. Avoid this issue by not using the NAMESPACE command if we detect that
			// the server is Microsoft Exchange 2003. See https://github.com/jstedfast/MailKit/issues/1512 for details.
			if (QuirksMode != ImapQuirksMode.Exchange2003 && (Capabilities & ImapCapabilities.Namespace) != 0) {
				ic = QueueCommand (cancellationToken, null, "NAMESPACE\r\n");

				await RunAsync (ic).ConfigureAwait (false);

				ProcessNamespaceResponse (ic);
			} else {
				var list = new List<ImapFolder> ();

				ic = QueueListNamespaceCommand (list, cancellationToken);

				await RunAsync (ic).ConfigureAwait (false);

				ProcessListNamespaceResponse (ic, list);

				await LookupParentFoldersAsync (list, cancellationToken).ConfigureAwait (false);
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

		ImapCommand QueueListInboxCommand (CancellationToken cancellationToken, out StringBuilder command, out List<ImapFolder> list)
		{
			bool returnsSubscribed = false;

			command = new StringBuilder ("LIST \"\" \"INBOX\"");
			list = new List<ImapFolder> ();

			if ((Capabilities & ImapCapabilities.ListExtended) != 0) {
				command.Append (" RETURN (SUBSCRIBED CHILDREN)");
				returnsSubscribed = true;
			}

			command.Append ("\r\n");

			var ic = new ImapCommand (this, cancellationToken, null, command.ToString ());
			ic.RegisterUntaggedHandler ("LIST", ImapUtils.UntaggedListHandler);
			ic.ListReturnsSubscribed = returnsSubscribed;
			ic.UserData = list;

			QueueCommand (ic);

			return ic;
		}

		void ProcessListInboxResponse (ImapCommand ic, StringBuilder command, List<ImapFolder> list)
		{
			TryGetCachedFolder ("INBOX", out var folder);
			Inbox = folder;

			command.Clear ();
			list.Clear ();
		}

		ImapCommand QueueListSpecialUseCommand (StringBuilder command, List<ImapFolder> list, CancellationToken cancellationToken)
		{
			bool returnsSubscribed = false;

			command.Append ("LIST ");

			// Note: Some IMAP servers like ProtonMail respond to SPECIAL-USE LIST queries with BAD, so fall
			// back to just issuing a standard LIST command and hope we get back some SPECIAL-USE attributes.
			//
			// See https://github.com/jstedfast/MailKit/issues/674 for dertails.
			if (QuirksMode != ImapQuirksMode.ProtonMail)
				command.Append ("(SPECIAL-USE) \"\" \"*\"");
			else
				command.Append ("\"\" \"%%\"");

			if ((Capabilities & ImapCapabilities.ListExtended) != 0) {
				command.Append (" RETURN (SUBSCRIBED CHILDREN)");
				returnsSubscribed = true;
			}

			command.Append ("\r\n");

			var ic = new ImapCommand (this, cancellationToken, null, command.ToString ());
			ic.RegisterUntaggedHandler ("LIST", ImapUtils.UntaggedListHandler);
			ic.ListReturnsSubscribed = returnsSubscribed;
			ic.UserData = list;

			QueueCommand (ic);

			return ic;
		}

		ImapCommand QueueXListCommand (List<ImapFolder> list, CancellationToken cancellationToken)
		{
			var ic = new ImapCommand (this, cancellationToken, null, "XLIST \"\" \"*\"\r\n");
			ic.RegisterUntaggedHandler ("XLIST", ImapUtils.UntaggedListHandler);
			ic.UserData = list;

			QueueCommand (ic);

			return ic;
		}

		/// <summary>
		/// Queries the special folders.
		/// </summary>
		/// <param name="cancellationToken">The cancellation token.</param>
		public void QuerySpecialFolders (CancellationToken cancellationToken)
		{
			var ic = QueueListInboxCommand (cancellationToken, out var command, out var list);

			Run (ic);

			ProcessListInboxResponse (ic, command, list);

			if ((Capabilities & ImapCapabilities.SpecialUse) != 0) {
				ic = QueueListSpecialUseCommand (command, list, cancellationToken);

				Run (ic);

				// Note: We specifically don't throw if we get a LIST error.
			} else if ((Capabilities & ImapCapabilities.XList) != 0) {
				ic = QueueXListCommand (list, cancellationToken);

				Run (ic);

				// Note: We specifically don't throw if we get a XLIST error.
			}

			LookupParentFolders (list, cancellationToken);

			AssignSpecialFolders (list);
		}

		/// <summary>
		/// Queries the special folders.
		/// </summary>
		/// <param name="cancellationToken">The cancellation token.</param>
		public async Task QuerySpecialFoldersAsync (CancellationToken cancellationToken)
		{
			var ic = QueueListInboxCommand (cancellationToken, out var command, out var list);

			await RunAsync (ic).ConfigureAwait (false);

			ProcessListInboxResponse (ic, command, list);

			if ((Capabilities & ImapCapabilities.SpecialUse) != 0) {
				ic = QueueListSpecialUseCommand (command, list, cancellationToken);

				await RunAsync (ic).ConfigureAwait (false);

				// Note: We specifically don't throw if we get a LIST error.
			} else if ((Capabilities & ImapCapabilities.XList) != 0) {
				ic = QueueXListCommand (list, cancellationToken);

				await RunAsync (ic).ConfigureAwait (false);

				// Note: We specifically don't throw if we get a LIST error.
			}

			await LookupParentFoldersAsync (list, cancellationToken).ConfigureAwait (false);

			AssignSpecialFolders (list);
		}

		ImapFolder ProcessGetQuotaRootResponse (ImapCommand ic, string quotaRoot, out List<ImapFolder> list)
		{
			ImapFolder folder;

			list = (List<ImapFolder>) ic.UserData;

			ic.ThrowIfNotOk ("LIST");

			if ((folder = GetFolder (list, quotaRoot)) == null) {
				folder = CreateImapFolder (quotaRoot, FolderAttributes.NonExistent, '.');
				CacheFolder (folder);
			}

			return folder;
		}

		/// <summary>
		/// Gets the folder representing the specified quota root.
		/// </summary>
		/// <returns>The folder.</returns>
		/// <param name="quotaRoot">The name of the quota root.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		public ImapFolder GetQuotaRootFolder (string quotaRoot, CancellationToken cancellationToken)
		{
			if (TryGetCachedFolder (quotaRoot, out var folder))
				return folder;

			var ic = QueueGetFolderCommand (quotaRoot, cancellationToken);

			Run (ic);

			folder = ProcessGetQuotaRootResponse (ic, quotaRoot, out var list);

			LookupParentFolders (list, cancellationToken);

			return folder;
		}

		/// <summary>
		/// Gets the folder representing the specified quota root.
		/// </summary>
		/// <returns>The folder.</returns>
		/// <param name="quotaRoot">The name of the quota root.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		public async Task<ImapFolder> GetQuotaRootFolderAsync (string quotaRoot, CancellationToken cancellationToken)
		{
			if (TryGetCachedFolder (quotaRoot, out var folder))
				return folder;

			var ic = QueueGetFolderCommand (quotaRoot, cancellationToken);

			await RunAsync (ic).ConfigureAwait (false);

			folder = ProcessGetQuotaRootResponse (ic, quotaRoot, out var list);

			await LookupParentFoldersAsync (list, cancellationToken).ConfigureAwait (false);

			return folder;
		}

		ImapCommand QueueGetFolderCommand (string encodedName, CancellationToken cancellationToken)
		{
			var command = new StringBuilder ("LIST \"\" %S");
			var list = new List<ImapFolder> ();
			var returnsSubscribed = false;

			if ((Capabilities & ImapCapabilities.ListExtended) != 0) {
				command.Append (" RETURN (SUBSCRIBED CHILDREN)");
				returnsSubscribed = true;
			}

			command.Append ("\r\n");

			var ic = new ImapCommand (this, cancellationToken, null, command.ToString (), encodedName);
			ic.RegisterUntaggedHandler ("LIST", ImapUtils.UntaggedListHandler);
			ic.ListReturnsSubscribed = returnsSubscribed;
			ic.UserData = list;

			QueueCommand (ic);

			return ic;
		}

		static ImapFolder ProcessGetFolderResponse (ImapCommand ic, string path, string encodedName, out List<ImapFolder> list)
		{
			ImapFolder folder;

			list = (List<ImapFolder>) ic.UserData;

			ic.ThrowIfNotOk ("LIST");

			if ((folder = GetFolder (list, encodedName)) == null)
				throw new FolderNotFoundException (path);

			return folder;
		}

		/// <summary>
		/// Gets the folder for the specified path.
		/// </summary>
		/// <returns>The folder.</returns>
		/// <param name="path">The folder path.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		public ImapFolder GetFolder (string path, CancellationToken cancellationToken)
		{
			var encodedName = EncodeMailboxName (path);

			if (TryGetCachedFolder (encodedName, out var folder))
				return folder;

			var ic = QueueGetFolderCommand (encodedName, cancellationToken);

			Run (ic);

			folder = ProcessGetFolderResponse (ic, path, encodedName, out var list);

			LookupParentFolders (list, cancellationToken);

			return folder;
		}

		/// <summary>
		/// Gets the folder for the specified path.
		/// </summary>
		/// <returns>The folder.</returns>
		/// <param name="path">The folder path.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		public async Task<IMailFolder> GetFolderAsync (string path, CancellationToken cancellationToken)
		{
			var encodedName = EncodeMailboxName (path);

			if (TryGetCachedFolder (encodedName, out var folder))
				return folder;

			var ic = QueueGetFolderCommand (encodedName, cancellationToken);

			await RunAsync (ic).ConfigureAwait (false);

			folder = ProcessGetFolderResponse (ic, path, encodedName, out var list);

			await LookupParentFoldersAsync (list, cancellationToken).ConfigureAwait (false);

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

		ImapCommand QueueGetFoldersCommand (FolderNamespace @namespace, StatusItems items, bool subscribedOnly, CancellationToken cancellationToken, out bool status)
		{
			var encodedName = EncodeMailboxName (@namespace.Path);
			var pattern = encodedName.Length > 0 ? encodedName + @namespace.DirectorySeparator : string.Empty;
			var list = new List<ImapFolder> ();
			var command = new StringBuilder ();
			var returnsSubscribed = false;
			var lsub = subscribedOnly;

			status = items != StatusItems.None;

			if (!TryGetCachedFolder (encodedName, out var folder))
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

					command.Append ("STATUS (");
					command.Append (GetStatusQuery (items));
					command.Append ("))");
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
			ic.RegisterUntaggedHandler (lsub ? "LSUB" : "LIST", ImapUtils.UntaggedListHandler);
			ic.ListReturnsSubscribed = returnsSubscribed;
			ic.UserData = list;
			ic.Lsub = lsub;

			QueueCommand (ic);

			return ic;
		}

		static IList<IMailFolder> ToListOfIMailFolder (List<ImapFolder> list)
		{
			var folders = new IMailFolder[list.Count];
			for (int i = 0; i < folders.Length; i++)
				folders[i] = list[i];

			return folders;
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
		/// <param name="cancellationToken">The cancellation token.</param>
		public IList<IMailFolder> GetFolders (FolderNamespace @namespace, StatusItems items, bool subscribedOnly, CancellationToken cancellationToken)
		{
			var ic = QueueGetFoldersCommand (@namespace, items, subscribedOnly, cancellationToken, out bool status);
			var list = (List<ImapFolder>) ic.UserData;

			Run (ic);

			ic.ThrowIfNotOk (ic.Lsub ? "LSUB" : "LIST");

			LookupParentFolders (list, cancellationToken);

			if (status) {
				for (int i = 0; i < list.Count; i++) {
					if (list[i].Exists)
						list[i].Status (items, false, cancellationToken);
				}
			}

			return ToListOfIMailFolder (list);
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
		/// <param name="cancellationToken">The cancellation token.</param>
		public async Task<IList<IMailFolder>> GetFoldersAsync (FolderNamespace @namespace, StatusItems items, bool subscribedOnly, CancellationToken cancellationToken)
		{
			var ic = QueueGetFoldersCommand (@namespace, items, subscribedOnly, cancellationToken, out bool status);
			var list = (List<ImapFolder>) ic.UserData;

			await RunAsync (ic).ConfigureAwait (false);

			ic.ThrowIfNotOk (ic.Lsub ? "LSUB" : "LIST");

			await LookupParentFoldersAsync (list, cancellationToken).ConfigureAwait (false);

			if (status) {
				for (int i = 0; i < list.Count; i++) {
					if (list[i].Exists)
						await list[i].StatusAsync (items, false, cancellationToken).ConfigureAwait (false);
				}
			}

			return ToListOfIMailFolder (list);
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
		public static bool IsValidMailboxName (string mailboxName, char delim)
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
				parser.SetStream (stream, persistent);
		}

		public HeaderList ParseHeaders (Stream stream, CancellationToken cancellationToken)
		{
			InitializeParser (stream, false);

			return parser.ParseHeaders (cancellationToken);
		}

		public Task<HeaderList> ParseHeadersAsync (Stream stream, CancellationToken cancellationToken)
		{
			InitializeParser (stream, false);

			return parser.ParseHeadersAsync (cancellationToken);
		}

		public MimeMessage ParseMessage (Stream stream, bool persistent, CancellationToken cancellationToken)
		{
			InitializeParser (stream, persistent);

			return parser.ParseMessage (cancellationToken);
		}

		public Task<MimeMessage> ParseMessageAsync (Stream stream, bool persistent, CancellationToken cancellationToken)
		{
			InitializeParser (stream, persistent);

			return parser.ParseMessageAsync (cancellationToken);
		}

		public MimeEntity ParseEntity (Stream stream, bool persistent, CancellationToken cancellationToken)
		{
			InitializeParser (stream, persistent);

			return parser.ParseEntity (cancellationToken);
		}

		public Task<MimeEntity> ParseEntityAsync (Stream stream, bool persistent, CancellationToken cancellationToken)
		{
			InitializeParser (stream, persistent);

			return parser.ParseEntityAsync (cancellationToken);
		}

		/// <summary>
		/// Occurs when the engine receives an alert message from the server.
		/// </summary>
		public event EventHandler<AlertEventArgs> Alert;

		internal void OnAlert (string message)
		{
			Alert?.Invoke (this, new AlertEventArgs (message));
		}

		/// <summary>
		/// Occurs when the engine receives a webalert message from the server.
		/// </summary>
		public event EventHandler<WebAlertEventArgs> WebAlert;

		internal void OnWebAlert (Uri uri, string message)
		{
			WebAlert?.Invoke (this, new WebAlertEventArgs (uri, message));
		}

		/// <summary>
		/// Occurs when the engine receives a notification that a folder has been created.
		/// </summary>
		public event EventHandler<FolderCreatedEventArgs> FolderCreated;

		internal void OnFolderCreated (IMailFolder folder)
		{
			FolderCreated?.Invoke (this, new FolderCreatedEventArgs (folder));
		}

		/// <summary>
		/// Occurs when the engine receives a notification that metadata has changed.
		/// </summary>
		public event EventHandler<MetadataChangedEventArgs> MetadataChanged;

		internal void OnMetadataChanged (Metadata metadata)
		{
			MetadataChanged?.Invoke (this, new MetadataChangedEventArgs (metadata));
		}

		/// <summary>
		/// Occurs when the engine receives a notification overflow message from the server.
		/// </summary>
		public event EventHandler<EventArgs> NotificationOverflow;

		internal void OnNotificationOverflow ()
		{
			// [NOTIFICATIONOVERFLOW] will reset to NOTIFY NONE
			NotifySelectedNewExpunge = false;

			NotificationOverflow?.Invoke (this, EventArgs.Empty);
		}

		public event EventHandler<EventArgs> Disconnected;

		void OnDisconnected ()
		{
			Disconnected?.Invoke (this, EventArgs.Empty);
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
			Disconnect (null);
		}
	}
}
