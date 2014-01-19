//
// ImapEngine.cs
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
using System.IO;
using System.Text;
using System.Threading;
using System.Diagnostics;
using System.Collections.Generic;

namespace MailKit.Net.Imap {
	/// <summary>
	/// The state of the <see cref="ImapEngine"/>.
	/// </summary>
	enum ImapEngineState {
		/// <summary>
		/// The ImapEngine is in the disconnected state.
		/// </summary>
		Disconnected,

		/// <summary>
		/// The ImapEngine is connected but has not authenticated.
		/// </summary>
		Connected,

		/// <summary>
		/// The ImapEngine is in the PREAUTH state.
		/// </summary>
		PreAuth,

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
		Idle,
	}

	enum ImapProtocolVersion {
		Unknown,
		Imap4,
		Imap4rev1
	}

	enum ImapUntaggedResult {
		Ok,
		No,
		Bad,
		Handled
	}

	/// <summary>
	/// An IMAP command engine.
	/// </summary>
	class ImapEngine
	{
		static readonly Encoding Latin1 = Encoding.GetEncoding (28591);
		internal readonly Dictionary<string, ImapFolder> FolderCache;
		internal readonly char TagPrefix = 'A';
		readonly List<ImapCommand> queue;
		ImapCommand current;
		ImapStream stream;
		internal int Tag;
		int nextId;

		public ImapEngine ()
		{
			FolderCache = new Dictionary<string, ImapFolder> ();
			AuthenticationMechanisms = new HashSet<string> ();
			CompressionAlgorithms = new HashSet<string> ();
			SupportedCharsets = new HashSet<string> ();
			SupportedCharsets.Add ("UTF-8");

			ProtocolVersion = ImapProtocolVersion.Unknown;
			Capabilities = ImapCapabilities.None;
			queue = new List<ImapCommand> ();
			nextId = 1;
		}

		/// <summary>
		/// Gets the authentication mechanisms supported by the IMAP server.
		/// </summary>
		/// <remarks>
		/// The authentication mechanisms are queried durring the
		/// <see cref="Connect"/> method.
		/// </remarks>
		/// <value>The authentication mechanisms.</value>
		public HashSet<string> AuthenticationMechanisms {
			get; private set;
		}

		/// <summary>
		/// Gets the compression algorithms supported by the IMAP server.
		/// </summary>
		/// <remarks>
		/// The compression algorithms are queried durring the
		/// <see cref="Connect"/> method.
		/// </remarks>
		/// <value>The compression algorithms.</value>
		public HashSet<string> CompressionAlgorithms {
			get; private set;
		}

		/// <summary>
		/// Gets the capabilities supported by the IMAP server.
		/// </summary>
		/// <remarks>
		/// The capabilities will not be known until a successful connection
		/// has been made via the <see cref="Connect"/> method.
		/// </remarks>
		/// <value>The capabilities.</value>
		public ImapCapabilities Capabilities {
			get; private set;
		}

		/// <summary>
		/// Gets the IMAP protocol version.
		/// </summary>
		/// <value>The IMAP protocol version.</value>
		public ImapProtocolVersion ProtocolVersion {
			get; private set;
		}

		/// <summary>
		/// Gets the supported charsets.
		/// </summary>
		/// <value>The supported charsets.</value>
		public HashSet<string> SupportedCharsets {
			get; private set;
		}

		/// <summary>
		/// Gets the underlying IMAP stream.
		/// </summary>
		/// <value>The pop3 stream.</value>
		public ImapStream Stream {
			get { return stream; }
		}

		/// <summary>
		/// Gets or sets the state of the engine.
		/// </summary>
		/// <value>The engine state.</value>
		public ImapEngineState State {
			get; set;
		}

		/// <summary>
		/// Gets whether or not the engine is currently connected to a IMAP server.
		/// </summary>
		/// <value><c>true</c> if the engine is connected; otherwise, <c>false</c>.</value>
		public bool IsConnected {
			get { return stream != null && stream.IsConnected; }
		}

		/// <summary>
		/// Gets the personal folder namespaces.
		/// </summary>
		/// <value>The personal folder namespaces.</value>
		public FolderNamespaceCollection PersonalNamespaces {
			get; private set;
		}

		/// <summary>
		/// Gets the shared folder namespaces.
		/// </summary>
		/// <value>The shared folder namespaces.</value>
		public FolderNamespaceCollection SharedNamespaces {
			get; private set;
		}

		/// <summary>
		/// Gets the other folder namespaces.
		/// </summary>
		/// <value>The other folder namespaces.</value>
		public FolderNamespaceCollection OtherNamespaces {
			get; private set;
		}

		/// <summary>
		/// Gets the selected folder.
		/// </summary>
		/// <value>The selected folder.</value>
		public ImapFolder Selected {
			get; private set;
		}

		internal static ImapException UnexpectedToken (ImapToken token, bool greeting)
		{
			string message;

			if (greeting)
				message = string.Format ("Unexpected token in IMAP greeting: {0}", token);
			else
				message = string.Format ("Unexpected token in IMAP response: {0}", token);

			return new ImapException (ImapErrorType.ParseError, message);
		}

		/// <summary>
		/// Takes posession of the <see cref="ImapStream"/> and reads the greeting.
		/// </summary>
		/// <param name="imap">The IMAP stream.</param>
		/// <param name="cancellationToken">A cancellation token</param>
		public void Connect (ImapStream imap, CancellationToken cancellationToken)
		{
			if (stream != null)
				stream.Dispose ();

			Capabilities = ImapCapabilities.None;
			AuthenticationMechanisms.Clear ();
			State = ImapEngineState.Connected;
			stream = imap;

			try {
				var token = stream.ReadToken (cancellationToken);

				if (token.Type != ImapTokenType.Asterisk)
					throw UnexpectedToken (token, true);

				token = stream.ReadToken (cancellationToken);

				if (token.Type != ImapTokenType.Atom)
					throw UnexpectedToken (token, true);

				var atom = (string) token.Value;

				switch (atom) {
				case "PREAUTH": State = ImapEngineState.Authenticated; break;
				case "OK":      State = ImapEngineState.PreAuth; break;
				case "BYE":
					// FIXME: should we throw a special exception here?
					throw UnexpectedToken (token, true);
				default:
					throw UnexpectedToken (token, true);
				}

				token = stream.ReadToken (cancellationToken);

				if (token.Type == ImapTokenType.OpenBracket) {
					var code = ParseResponseCode (cancellationToken);
					if (code.Type == ImapResponseCodeType.Alert)
						OnAlert (code.Message);
				} else if (token.Type != ImapTokenType.Eoln) {
					// throw away any remaining text up until the end of the line
					ReadLine (cancellationToken);
				}
			} catch {
				Disconnect ();
				throw;
			}
		}

		/// <summary>
		/// Disconnects the <see cref="ImapStream"/>.
		/// </summary>
		public void Disconnect ()
		{
			State = ImapEngineState.Disconnected;

			if (stream != null) {
				stream.Dispose ();
				stream = null;
			}
		}

		/// <summary>
		/// Reads a single line from the <see cref="ImapStream"/>.
		/// </summary>
		/// <returns>The line.</returns>
		/// <param name="cancellationToken">A cancellation token.</param>
		/// <exception cref="System.InvalidOperationException">
		/// The engine is not connected.
		/// </exception>
		/// <exception cref="System.OperationCanceledException">
		/// The operation was canceled via the cancellation token.
		/// </exception>
		/// <exception cref="System.IO.IOException">
		/// An I/O error occurred.
		/// </exception>
		public string ReadLine (CancellationToken cancellationToken)
		{
			if (stream == null)
				throw new InvalidOperationException ();

			cancellationToken.ThrowIfCancellationRequested ();

			using (var memory = new MemoryStream ()) {
				int offset, count;
				byte[] buf;

				while (!stream.ReadLine (out buf, out offset, out count)) {
					cancellationToken.ThrowIfCancellationRequested ();
					memory.Write (buf, offset, count);
				}

				memory.Write (buf, offset, count);

				count = (int) memory.Length;
				buf = memory.GetBuffer ();

				try {
					return Encoding.UTF8.GetString (buf, 0, count);
				} catch {
					return Latin1.GetString (buf, 0, count);
				}
			}
		}

		internal void SkipLine (CancellationToken cancellationToken)
		{
			ImapToken token;

			do {
				token = stream.ReadToken (cancellationToken);

				if (token.Type == ImapTokenType.Literal) {
					var buf = new byte[4096];
					int nread;

					do {
						cancellationToken.ThrowIfCancellationRequested ();
						nread = stream.Read (buf, 0, buf.Length);
					} while (nread > 0);
				}
			} while (token.Type != ImapTokenType.Eoln);
		}

		void UpdateCapabilities (ImapTokenType sentinel, CancellationToken cancellationToken)
		{
			ProtocolVersion = ImapProtocolVersion.Unknown;
			Capabilities = ImapCapabilities.None;
			AuthenticationMechanisms.Clear ();

			var token = stream.ReadToken (cancellationToken);

			while (token.Type == ImapTokenType.Atom) {
				var atom = (string) token.Value;

				if (atom.StartsWith ("AUTH=", StringComparison.Ordinal)) {
					AuthenticationMechanisms.Add (atom.Substring ("AUTH=".Length));
				} else if (atom.StartsWith ("COMPRESS=", StringComparison.Ordinal)) {
					CompressionAlgorithms.Add (atom.Substring ("COMPRESS=".Length));
					Capabilities |= ImapCapabilities.Compress;
				} else {
					switch (atom.ToUpperInvariant ()) {
					case "IMAP4":         Capabilities |= ImapCapabilities.IMAP4; break;
					case "IMAP4REV1":     Capabilities |= ImapCapabilities.IMAP4rev1; break;
					case "STATUS":        Capabilities |= ImapCapabilities.Status; break;
					case "QUOTA":         Capabilities |= ImapCapabilities.Quota; break;
					case "LITERAL+":      Capabilities |= ImapCapabilities.LiteralPlus; break;
					case "IDLE":          Capabilities |= ImapCapabilities.Idle; break;
					case "NAMESPACE":     Capabilities |= ImapCapabilities.Namespace; break;
					case "CHILDREN":      Capabilities |= ImapCapabilities.Children; break;
					case "LOGINDISABLED": Capabilities |= ImapCapabilities.LoginDisabled; break;
					case "STARTTLS":      Capabilities |= ImapCapabilities.StartTLS; break;
					case "MULTIAPPEND":   Capabilities |= ImapCapabilities.MultiAppend; break;
					case "BINARY":        Capabilities |= ImapCapabilities.Binary; break;
					case "UNSELECT":      Capabilities |= ImapCapabilities.Unselect; break;
					case "UIDPLUS":       Capabilities |= ImapCapabilities.UidPlus; break;
					case "CATENATE":      Capabilities |= ImapCapabilities.Catenate; break;
					case "CONDSTORE":     Capabilities |= ImapCapabilities.CondStore; break;
					case "ESEARCH":       Capabilities |= ImapCapabilities.ESearch; break;
					case "ENABLE":        Capabilities |= ImapCapabilities.Enable; break;
					case "LIST-EXTENDED": Capabilities |= ImapCapabilities.ListExtended; break;
					case "CONVERT":       Capabilities |= ImapCapabilities.Convert; break;
					case "METADATA":      Capabilities |= ImapCapabilities.MetaData; break;
					case "SPECIAL-USE":   Capabilities |= ImapCapabilities.SpecialUse; break;
					case "MOVE":          Capabilities |= ImapCapabilities.Move; break;
					}
				}

				token = stream.ReadToken (cancellationToken);
			}

			if (token.Type != sentinel) {
				Debug.WriteLine ("Expected {0} at the end of the CAPABILITIES, but got: {1}", sentinel, token);
				throw UnexpectedToken (token, false);
			}

			// unget the sentinel
			stream.UngetToken (token);

			if ((Capabilities & ImapCapabilities.IMAP4rev1) != 0) {
				ProtocolVersion = ImapProtocolVersion.Imap4rev1;
				Capabilities |= ImapCapabilities.Status;
			} else if ((Capabilities & ImapCapabilities.IMAP4) != 0) {
				ProtocolVersion = ImapProtocolVersion.Imap4;
			}
		}

		public MessageFlags ParseFlagsList (CancellationToken cancellationToken)
		{
			var token = stream.ReadToken (cancellationToken);
			var flags = MessageFlags.None;

			if (token.Type != ImapTokenType.OpenParen) {
				Debug.WriteLine ("Expected '(' at the start of the flags list, but got: {0}", token);
				throw UnexpectedToken (token, false);
			}

			token = stream.ReadToken (cancellationToken);

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

				token = stream.ReadToken (cancellationToken);
			}

			if (token.Type !=  ImapTokenType.CloseParen) {
				Debug.WriteLine ("Expected to find a ')' token terminating the flags list, but got: {0}", token);
				throw UnexpectedToken (token, false);
			}

			return flags;
		}

		void UpdateNamespaces (CancellationToken cancellationToken)
		{
			var namespaces = new List<FolderNamespaceCollection> () {
				PersonalNamespaces, SharedNamespaces, OtherNamespaces
			};
			ImapToken token;
			string path;
			char dirsep;
			int n = 0;

			PersonalNamespaces.Clear ();
			SharedNamespaces.Clear ();
			OtherNamespaces.Clear ();

			token = stream.ReadToken (cancellationToken);

			do {
				if (token.Type == ImapTokenType.OpenParen) {
					// parse the list of namespace pairs...
					token = stream.ReadToken (cancellationToken);

					while (token.Type == ImapTokenType.OpenParen) {
						// parse the namespace pair - first token is the path
						token = stream.ReadToken (cancellationToken);

						if (token.Type != ImapTokenType.QString) {
							Debug.WriteLine ("Expected qstring token as first element in namespace pair, but got: {0}", token);
							throw UnexpectedToken (token, false);
						}

						path = (string) token.Value;

						// second token is the directory separator
						token = stream.ReadToken (cancellationToken);

						if (token.Type != ImapTokenType.QString) {
							Debug.WriteLine ("Expected qstring token as second element in namespace pair, but got: {0}", token);
							throw UnexpectedToken (token, false);
						}

						dirsep = ((string) token.Value)[0];

						// canonicalize the namespace path
						path = path.TrimEnd (dirsep);

						namespaces[n].Add (new FolderNamespace (dirsep, path));

						// read the closing ')'
						token = stream.ReadToken (cancellationToken);

						if (token.Type != ImapTokenType.CloseParen) {
							Debug.WriteLine ("Expected ')' to close namespace pair, but got: {0}", token);
							throw UnexpectedToken (token, false);
						}

						// read the next token - it should either be '(' or ')'
						token = stream.ReadToken (cancellationToken);
					}

					if (token.Type != ImapTokenType.CloseParen) {
						Debug.WriteLine ("Expected ')' to close namespace pair, but got: {0}", token);
						throw UnexpectedToken (token, false);
					}
				} else if (token.Type != ImapTokenType.Nil) {
					Debug.WriteLine ("Expected '(' or 'NIL' token after untagged 'NAMESPACE' response, but got: {0}", token);
					throw UnexpectedToken (token, false);
				}

				token = stream.ReadToken (cancellationToken);
				n++;
			} while (n < 3);

			while (token.Type != ImapTokenType.Eoln)
				token = stream.ReadToken (cancellationToken);
		}

		static ImapResponseCodeType GetResponseCodeType (string atom)
		{
			switch (atom) {
			case "ALERT":          return ImapResponseCodeType.Alert;
			case "BADCHARSET":     return ImapResponseCodeType.BadCharset;
			case "CAPABILITY":     return ImapResponseCodeType.Capability;
			case "PARSE":          return ImapResponseCodeType.Parse;
			case "PERMANENTFLAGS": return ImapResponseCodeType.PermanentFlags;
			case "READONLY":       return ImapResponseCodeType.ReadOnly;
			case "READWRITE":      return ImapResponseCodeType.ReadWrite;
			case "TRYCREATE":      return ImapResponseCodeType.TryCreate;
			case "UIDNEXT":        return ImapResponseCodeType.UidNext;
			case "UIDVALIDITY":    return ImapResponseCodeType.UidValidity;
			case "UNSEEN":         return ImapResponseCodeType.Unseen;
			case "NEWNAME":        return ImapResponseCodeType.NewName;
			case "APPENDUID":      return ImapResponseCodeType.AppendUid;
			case "COPYUID":        return ImapResponseCodeType.CopyUid;
			case "UIDNOTSTICKY":   return ImapResponseCodeType.UidNotSticky;
			case "HIGHESTMODSEQ":  return ImapResponseCodeType.HighestModSeq;
			case "NOMODSEQ":       return ImapResponseCodeType.NoModSeq;
			default:               return ImapResponseCodeType.Unknown;
			}
		}

		public ImapResponseCode ParseResponseCode (CancellationToken cancellationToken)
		{
			ImapResponseCode code;
			ImapToken token;
			string atom;
			ulong n64;
			uint n32;

//			token = stream.ReadToken (cancellationToken);
//
//			if (token.Type != ImapTokenType.LeftBracket) {
//				Debug.WriteLine ("Expected a '[' followed by a RESP-CODE, but got: {0}", token);
//				throw UnexpectedToken (token, false);
//			}

			token = stream.ReadToken (cancellationToken);

			if (token.Type != ImapTokenType.Atom) {
				Debug.WriteLine ("Expected an atom token containing a RESP-CODE, but got: {0}", token);
				throw UnexpectedToken (token, false);
			}

			atom = (string) token.Value;
			token = stream.ReadToken (cancellationToken);

			code = new ImapResponseCode (GetResponseCodeType (atom));

			switch (code.Type) {
			case ImapResponseCodeType.Alert:
				if (token.Type != ImapTokenType.CloseBracket) {
					Debug.WriteLine ("Expected ']' after 'ALERT' RESP-CODE, but got: {0}", token);
					throw UnexpectedToken (token, false);
				}

				code.Message = ReadLine (cancellationToken).Trim ();

				return code;
			case ImapResponseCodeType.BadCharset:
				if (token.Type == ImapTokenType.OpenParen) {
					token = stream.ReadToken (cancellationToken);

					SupportedCharsets.Clear ();
					while (token.Type == ImapTokenType.Atom || token.Type == ImapTokenType.QString) {
						SupportedCharsets.Add ((string) token.Value);
						token = stream.ReadToken (cancellationToken);
					}

					if (token.Type != ImapTokenType.CloseParen) {
						Debug.WriteLine ("Expected ')' after list of charsets in 'BADCHARSET' RESP-CODE, but got: {0}", token);
						throw UnexpectedToken (token, false);
					}

					token = stream.ReadToken (cancellationToken);
				}
				break;
			case ImapResponseCodeType.Capability:
				UpdateCapabilities (ImapTokenType.CloseBracket, cancellationToken);
				break;
			case ImapResponseCodeType.Parse:
				if (token.Type != ImapTokenType.CloseBracket) {
					Debug.WriteLine ("Expected ']' after 'PARSE' RESP-CODE, but got: {0}", token);
					throw UnexpectedToken (token, false);
				}

				code.Message = ReadLine (cancellationToken).Trim ();

				return code;
			case ImapResponseCodeType.PermanentFlags:
				code.Flags = ParseFlagsList (cancellationToken);
				token = stream.ReadToken (cancellationToken);
				break;
			case ImapResponseCodeType.ReadOnly: break;
			case ImapResponseCodeType.ReadWrite: break;
			case ImapResponseCodeType.TryCreate: break;
			case ImapResponseCodeType.UidNext:
				if (token.Type != ImapTokenType.Atom || !uint.TryParse ((string) token.Value, out n32) || n32 == 0) {
					Debug.WriteLine ("Expected nz-number argument to 'UIDNEXT' RESP-CODE, but got: {0}", token);
					throw UnexpectedToken (token, false);
				}

				code.Uid = n32;

				token = stream.ReadToken (cancellationToken);
				break;
			case ImapResponseCodeType.UidValidity:
				if (token.Type != ImapTokenType.Atom || !uint.TryParse ((string) token.Value, out n32) || n32 == 0) {
					Debug.WriteLine ("Expected nz-number argument to 'UIDVALIDITY' RESP-CODE, but got: {0}", token);
					throw UnexpectedToken (token, false);
				}

				code.UidValidity = n32;

				token = stream.ReadToken (cancellationToken);
				break;
			case ImapResponseCodeType.Unseen:
				if (token.Type != ImapTokenType.Atom || !uint.TryParse ((string) token.Value, out n32) || n32 == 0) {
					Debug.WriteLine ("Expected nz-number argument to 'UNSEEN' RESP-CODE, but got: {0}", token);
					throw UnexpectedToken (token, false);
				}

				code.Index = (int) (n32 - 1);

				token = stream.ReadToken (cancellationToken);
				break;
			case ImapResponseCodeType.NewName:
				// Note: this RESP-CODE existed in rfc2060 but has been removed in rfc3501:
				//
				// 85) Remove NEWNAME.  It can't work because mailbox names can be
				// literals and can include "]".  Functionality can be addressed via
				// referrals.
				if (token.Type != ImapTokenType.Atom && token.Type != ImapTokenType.QString) {
					Debug.WriteLine ("Expected atom or qstring as first argument to 'NEWNAME' RESP-CODE, but got: {0}", token);
					throw UnexpectedToken (token, false);
				}

				// FIXME:
				//code.OldName = (string) token.Value;

				// the next token should be another atom or qstring token representing the new name of the folder
				token = stream.ReadToken (cancellationToken);

				if (token.Type != ImapTokenType.Atom && token.Type != ImapTokenType.QString) {
					Debug.WriteLine ("Expected atom or qstring as second argument to 'NEWNAME' RESP-CODE, but got: {0}", token);
					throw UnexpectedToken (token, false);
				}

				// FIXME:
				//code.NewName = (string) token.Value;

				token = stream.ReadToken (cancellationToken);
				break;
			case ImapResponseCodeType.AppendUid:
				if (token.Type != ImapTokenType.Atom || !uint.TryParse ((string) token.Value, out n32) || n32 == 0) {
					Debug.WriteLine ("Expected nz-number as first argument of the 'APPENDUID' RESP-CODE, but got: {0}", token);
					throw UnexpectedToken (token, false);
				}

				code.UidValidity = n32;

				token = stream.ReadToken (cancellationToken);

				if (token.Type != ImapTokenType.Atom || !uint.TryParse ((string) token.Value, out n32) || n32 == 0) {
					Debug.WriteLine ("Expected nz-number as second argument to 'APPENDUID' RESP-CODE, but got: {0}", token);
					throw UnexpectedToken (token, false);
				}

				code.Uid = n32;

				token = stream.ReadToken (cancellationToken);
				break;
			case ImapResponseCodeType.CopyUid:
				if (token.Type != ImapTokenType.Atom || !uint.TryParse ((string) token.Value, out n32) || n32 == 0) {
					Debug.WriteLine ("Expected nz-number as first argument of the 'COPYUID' RESP-CODE, but got: {0}", token);
					throw UnexpectedToken (token, false);
				}

				code.UidValidity = n32;

				token = stream.ReadToken (cancellationToken);

				if (token.Type != ImapTokenType.Atom) {
					Debug.WriteLine ("Expected atom as second argument to 'COPYUID' RESP-CODE, but got: {0}", token);
					throw UnexpectedToken (token, false);
				}

				code.SrcSet = (string) token.Value;

				token = stream.ReadToken (cancellationToken);

				if (token.Type != ImapTokenType.Atom) {
					Debug.WriteLine ("Expected atom as third argument to 'COPYUID' RESP-CODE, but got: {0}", token);
					throw UnexpectedToken (token, false);
				}

				code.DestSet = (string) token.Value;

				token = stream.ReadToken (cancellationToken);
				break;
			case ImapResponseCodeType.UidNotSticky: break;
			case ImapResponseCodeType.HighestModSeq:
				if (token.Type != ImapTokenType.Atom || !ulong.TryParse ((string) token.Value, out n64) || n64 == 0) {
					Debug.WriteLine ("Expected 64-bit nz-number as first argument of the 'HIGHESTMODSEQ' RESP-CODE, but got: {0}", token);
					throw UnexpectedToken (token, false);
				}

				code.HighestModSeq = n64;

				token = stream.ReadToken (cancellationToken);
				break;
			case ImapResponseCodeType.NoModSeq: break;
			default:
				code = new ImapResponseCode (ImapResponseCodeType.Unknown);

				Debug.WriteLine ("Unknown RESP-CODE encountered: {0}", atom);

				// extensions are of the form: "[" atom [SPACE 1*<any TEXT_CHAR except "]">] "]"

				// skip over tokens until we get to a ']'
				while (token.Type != ImapTokenType.CloseBracket && token.Type != ImapTokenType.Eoln)
					token = stream.ReadToken (cancellationToken);

				break;
			}

			if (token.Type != ImapTokenType.CloseBracket) {
				Debug.WriteLine ("Expected ']' after '{0}' RESP-CODE, but got: {1}", atom, token);
				throw UnexpectedToken (token, false);
			}

			// ignore any text after the response code
			ReadLine (cancellationToken);

			return code;
		}

		internal ImapUntaggedResult ProcessUntaggedResponse (CancellationToken cancellationToken)
		{
			var result = ImapUntaggedResult.Handled;
			var token = stream.ReadToken (cancellationToken);
			ImapUntaggedHandler handler;
			ImapFolder folder;
			int number;

			if (current != null && current.Folder != null)
				folder = current.Folder;
			else
				folder = Selected;

			if (token.Type == ImapTokenType.Atom) {
				var atom = (string) token.Value;

				switch (atom) {
				case "BYE":
					token = stream.ReadToken (cancellationToken);

					if (token.Type == ImapTokenType.OpenBracket) {
						var code = ParseResponseCode (cancellationToken);
						if (current != null)
							current.RespCodes.Add (code);
					}

					ReadLine (cancellationToken);

					if (current != null) {
						current.Bye = true;
					} else {
						Disconnect ();
					}
					break;
				case "CAPABILITY":
					UpdateCapabilities (ImapTokenType.Eoln, cancellationToken);

					// read the eoln token
					stream.ReadToken (cancellationToken);
					break;
				case "FLAGS":
					folder.AcceptedFlags = ParseFlagsList (cancellationToken);
					token = stream.ReadToken (cancellationToken);

					if (token.Type != ImapTokenType.Eoln) {
						Debug.WriteLine ("Expected eoln after untagged FLAGS list, but got: {0}", token);
						throw UnexpectedToken (token, false);
					}
					break;
				case "NAMESPACE":
					UpdateNamespaces (cancellationToken);
					break;
				case "NO": case "BAD":
					// our command got rejected...
					result = atom == "NO" ? ImapUntaggedResult.No : ImapUntaggedResult.Bad;

					token = stream.ReadToken (cancellationToken);

					if (token.Type == ImapTokenType.OpenBracket) {
						var code = ParseResponseCode (cancellationToken);
						if (current != null)
							current.RespCodes.Add (code);
					} else if (token.Type != ImapTokenType.Eoln) {
						// throw away any remaining text up until the end of the line
						ReadLine (cancellationToken);
					}
					break;
				case "OK":
					result = ImapUntaggedResult.Ok;

					token = stream.ReadToken (cancellationToken);

					if (token.Type == ImapTokenType.OpenBracket) {
						var code = ParseResponseCode (cancellationToken);
						if (current != null)
							current.RespCodes.Add (code);
					} else if (token.Type != ImapTokenType.Eoln) {
						// throw away any remaining text up until the end of the line
						ReadLine (cancellationToken);
					}
					break;
				default:
					if (int.TryParse (atom, out number)) {
						// we probably have something like "* 1 EXISTS"
						token = stream.ReadToken (cancellationToken);

						if (token.Type != ImapTokenType.Atom) {
							// protocol error
							Debug.WriteLine ("Unhandled untagged response: * {0} {1}", number, atom);
							// FIXME: throw an exception?
						}

						atom = (string) token.Value;

						switch (atom) {
						case "EXISTS":
							folder.UpdateCount (number);
							break;
						case "EXPUNGE":
							folder.OnExpunged (number);
							break;
						case "RECENT":
							folder.UpdateRecent (number);
							break;
						default:
							if (current != null && current.UntaggedHandlers.TryGetValue (atom, out handler)) {
								// the command registered an untagged handler for this atom...
								handler (this, current, token);
							} else {
								Debug.WriteLine ("Unhandled untagged response: * {0} {1}", number, atom);
							}
							break;
						}

						SkipLine (cancellationToken);
					} else if (current != null && current.UntaggedHandlers.TryGetValue (atom, out handler)) {
						// the command registered an untagged handler for this atom...
						handler (this, current, token);
					} else {
						// don't know how to handle this... eat it?
						SkipLine (cancellationToken);
					}
					break;
				}
			}

			return result;
		}

		public int Iterate ()
		{
			if (stream == null)
				throw new InvalidOperationException ();

			if (queue.Count == 0)
				return 0;

			current = queue[0];
			queue.RemoveAt (0);

			try {
				current.CancellationToken.ThrowIfCancellationRequested ();
			} catch (OperationCanceledException) {
				// FIXME: is this right??
				queue.RemoveAll (x => x.CancellationToken == current.CancellationToken);
				throw;
			}

			current.Status = ImapCommandStatus.Active;

			try {
				while (current.Step ()) {
					// more literal data to send...
				}

				// TODO: Update selected folder state after a SELECT, EXAMINE, CLOSE, and UNSELECT

				if (current.Bye)
					Disconnect ();
			} catch (ImapException ex) {
				// FIXME: not all ImapExceptions are fatal
				Disconnect ();
				throw;
			} catch {
				Disconnect ();
				throw;
			}

			return current.Id;
		}

		public ImapCommand QueueCommand (CancellationToken cancellationToken, ImapFolder folder, string format, params object[] args)
		{
			var ic = new ImapCommand (this, cancellationToken, folder, format, args);
			ic.Id = nextId++;
			queue.Add (ic);
			return ic;
		}

		public ImapCommandResult QueryCapabilities (CancellationToken cancellationToken)
		{
			if (stream == null)
				throw new InvalidOperationException ();

			var ic = QueueCommand (cancellationToken, null, "CAPABILITY\r\n");

			while (Iterate () < ic.Id) {
				// continue processing commands...
			}

			return ic.Result;
		}

		//public event EventHandler<ImapAlertEventArgs> Alert;

		void OnAlert (string message)
		{
//			var handler = Alert;
//
//			if (handler != null)
//				handler (this, new ImapAlertEventArgs (message));
		}
	}
}
