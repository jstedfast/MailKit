//
// ImapCommand.cs
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
using System.IO;
using System.Text;
using System.Threading;
using System.Diagnostics;
using System.Collections.Generic;

using MimeKit;
using MimeKit.IO;
using MimeKit.Utils;

#if NETFX_CORE
using Windows.Storage.Streams;
using Encoding = Portable.Text.Encoding;
#endif

namespace MailKit.Net.Imap {
	/// <summary>
	/// An IMAP continuation handler.
	/// </summary>
	/// <remarks>
	/// All exceptions thrown by the handler are considered fatal and will
	/// force-disconnect the connection. If a non-fatal error occurs, set
	/// it on the <see cref="ImapCommand.Exception"/> property.
	/// </remarks>
	delegate void ImapContinuationHandler (ImapEngine engine, ImapCommand ic, string text);

	/// <summary>
	/// An IMAP untagged response handler.
	/// </summary>
	/// <remarks>
	/// <para>Most IMAP commands return their results in untagged responses.</para>
	/// </remarks>
	delegate void ImapUntaggedHandler (ImapEngine engine, ImapCommand ic, int index);

	delegate void ImapCommandResetHandler (ImapCommand ic);

	/// <summary>
	/// IMAP command status.
	/// </summary>
	enum ImapCommandStatus {
		Created,
		Queued,
		Active,
		Complete,
		Error
	}

	enum ImapCommandResult {
		None,
		Ok,
		No,
		Bad
	}

	enum ImapLiteralType {
		String,
		Stream,
		MimeMessage
	}

	enum ImapStringType {
		Atom,
		QString,
		Literal
	}

	class ImapIdleContext : IDisposable
	{
		readonly CancellationTokenSource source;

		public ImapIdleContext (ImapEngine engine, CancellationToken doneToken, CancellationToken cancellationToken)
		{
			source = CancellationTokenSource.CreateLinkedTokenSource (doneToken, cancellationToken);
			CancellationToken = cancellationToken;
			DoneToken = doneToken;
			Engine = engine;
		}

		public ImapEngine Engine {
			get; private set;
		}

		public CancellationToken CancellationToken {
			get; private set;
		}

		public CancellationToken LinkedToken {
			get { return source.Token; }
		}

		public CancellationToken DoneToken {
			get; private set;
		}

		public bool IsCancellationRequested {
			get { return CancellationToken.IsCancellationRequested; }
		}

		public bool IsDoneRequested {
			get { return DoneToken.IsCancellationRequested; }
		}

		public void Dispose ()
		{
			source.Dispose ();
		}
	}

	/// <summary>
	/// An IMAP literal object.
	/// </summary>
	class ImapLiteral
	{
		public readonly ImapLiteralType Type;
		public readonly object Literal;
		readonly FormatOptions format;

		public ImapLiteral (FormatOptions options, object literal)
		{
			format = options;

			if (literal is MimeMessage) {
				Type = ImapLiteralType.MimeMessage;
			} else if (literal is Stream) {
				Type = ImapLiteralType.Stream;
			} else if (literal is string) {
				literal = Encoding.UTF8.GetBytes ((string) literal);
				Type = ImapLiteralType.String;
			} else if (literal is byte[]) {
				Type = ImapLiteralType.String;
			} else {
				throw new ArgumentException ("Unknown literal type");
			}

			Literal = literal;
		}

		/// <summary>
		/// Gets the length of the literal, in bytes.
		/// </summary>
		/// <value>The length.</value>
		public long Length {
			get {
				if (Type == ImapLiteralType.String)
					return (long) ((byte[]) Literal).Length;

				using (var measure = new MeasuringStream ()) {
					switch (Type) {
					case ImapLiteralType.Stream:
						var stream = (Stream) Literal;
						stream.CopyTo (measure, 4096);
						stream.Position = 0;
						break;
					case ImapLiteralType.MimeMessage:
						((MimeMessage) Literal).WriteTo (format, measure);
						break;
					}

					return measure.Length;
				}
			}
		}

		/// <summary>
		/// Writes the literal to the specified stream.
		/// </summary>
		/// <param name="stream">The stream.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		public void WriteTo (ImapStream stream, CancellationToken cancellationToken)
		{
			if (Type == ImapLiteralType.String) {
				var bytes = (byte[]) Literal;
				stream.Write (bytes, 0, bytes.Length, cancellationToken);
				stream.Flush (cancellationToken);
				return;
			}

			if (Type == ImapLiteralType.MimeMessage) {
				var message = (MimeMessage) Literal;

				message.WriteTo (format, stream, cancellationToken);
				stream.Flush (cancellationToken);
				return;
			}

			var literal = (Stream) Literal;
			var buf = new byte[4096];
			int nread;

			while ((nread = literal.Read (buf, 0, buf.Length)) > 0)
				stream.Write (buf, 0, nread, cancellationToken);

			stream.Flush (cancellationToken);
		}
	}

	/// <summary>
	/// A partial IMAP command.
	/// </summary>
	class ImapCommandPart
	{
		public readonly byte[] Command;
		public readonly ImapLiteral Literal;

		public ImapCommandPart (byte[] command, ImapLiteral literal)
		{
			Command = command;
			Literal = literal;
		}
	}

	/// <summary>
	/// An IMAP command.
	/// </summary>
	class ImapCommand
	{
		public Dictionary<string, ImapUntaggedHandler> UntaggedHandlers { get; private set; }
		public ImapContinuationHandler ContinuationHandler { get; set; }
		public CancellationToken CancellationToken { get; private set; }
		public ImapCommandStatus Status { get; internal set; }
		public ImapCommandResult Result { get; internal set; }
		public Exception Exception { get; internal set; }
		public readonly List<ImapResponseCode> RespCodes;
		public string ResultText { get; internal set; }
		public ImapFolder Folder { get; private set; }
		public object UserData { get; internal set; }
		public string Tag { get; private set; }
		public bool Bye { get; internal set; }
		public int Id { get; internal set; }

		readonly List<ImapCommandPart> parts = new List<ImapCommandPart> ();
		readonly ImapEngine Engine;
		int current;

		/// <summary>
		/// Initializes a new instance of the <see cref="MailKit.Net.Imap.ImapCommand"/> class.
		/// </summary>
		/// <param name="engine">The IMAP engine that will be sending the command.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <param name="folder">The IMAP folder that the command operates on.</param>
		/// <param name="options">The formatting options.</param>
		/// <param name="format">The command format.</param>
		/// <param name="args">The command arguments.</param>
		public ImapCommand (ImapEngine engine, CancellationToken cancellationToken, ImapFolder folder, FormatOptions options, string format, params object[] args)
		{
			UntaggedHandlers = new Dictionary<string, ImapUntaggedHandler> ();
			RespCodes = new List<ImapResponseCode> ();
			CancellationToken = cancellationToken;
			Status = ImapCommandStatus.Created;
			Result = ImapCommandResult.None;
			Engine = engine;
			Folder = folder;

			using (var builder = new MemoryStream ()) {
				var plus = (Engine.Capabilities & ImapCapabilities.LiteralPlus) != 0 ? "+" : string.Empty;
				int argc = 0;
				byte[] buf;
				string str;
				char c;

				for (int i = 0; i < format.Length; i++) {
					if (format[i] == '%') {
						switch (format[++i]) {
						case '%': // a literal %
							builder.WriteByte ((byte) '%');
							break;
						case 'c': // a character
							c = (char) args[argc++];
							builder.WriteByte ((byte) c);
							break;
						case 'd': // an integer
							str = ((int) args[argc++]).ToString ();
							buf = Encoding.ASCII.GetBytes (str);
							builder.Write (buf, 0, buf.Length);
							break;
						case 'u': // an unsigned integer
							str = ((uint) args[argc++]).ToString ();
							buf = Encoding.ASCII.GetBytes (str);
							builder.Write (buf, 0, buf.Length);
							break;
						case 'F': // an ImapFolder
							var utf7 = ((ImapFolder) args[argc++]).EncodedName;
							AppendString (options, builder, utf7);
							break;
						case 'L':
							var literal = new ImapLiteral (options, args[argc++]);
							var length = literal.Length;

							if (options.International)
								str = "UTF8 (~{" + length + plus + "}\r\n";
							else
								str = "{" + length + plus + "}\r\n";

							buf = Encoding.ASCII.GetBytes (str);
							builder.Write (buf, 0, buf.Length);

							parts.Add (new ImapCommandPart (builder.ToArray (), literal));
							builder.SetLength (0);

							if (options.International)
								builder.WriteByte ((byte) ')');
							break;
						case 'S': // a string which may need to be quoted or made into a literal
							AppendString (options, builder, (string) args[argc++]);
							break;
						case 's': // a safe atom string
							buf = Encoding.ASCII.GetBytes ((string) args[argc++]);
							builder.Write (buf, 0, buf.Length);
							break;
						default:
							throw new FormatException ();
						}
					} else {
						builder.WriteByte ((byte) format[i]);
					}
				}

				parts.Add (new ImapCommandPart (builder.ToArray (), null));
			}
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="MailKit.Net.Imap.ImapCommand"/> class.
		/// </summary>
		/// <param name="engine">The IMAP engine that will be sending the command.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <param name="folder">The IMAP folder that the command operates on.</param>
		/// <param name="format">The command format.</param>
		/// <param name="args">The command arguments.</param>
		public ImapCommand (ImapEngine engine, CancellationToken cancellationToken, ImapFolder folder, string format, params object[] args)
			: this (engine, cancellationToken, folder, FormatOptions.Default, format, args)
		{
		}

		static bool IsAtom (char c)
		{
			return c < 128 && !char.IsControl (c) && "(){ \t%*\\\"]".IndexOf (c) == -1;
		}

		bool IsQuotedSafe (char c)
		{
			return (c < 128 || Engine.UTF8Enabled) && !char.IsControl (c);
		}

		ImapStringType GetStringType (string value)
		{
			var type = ImapStringType.Atom;

			for (int i = 0; i < value.Length; i++) {
				if (!IsAtom (value[i])) {
					if (!IsQuotedSafe (value[i]))
						return ImapStringType.Literal;

					type = ImapStringType.QString;
				}
			}

			return type;
		}

		void AppendString (FormatOptions options, MemoryStream builder, string value)
		{
			byte[] buf;

			switch (GetStringType (value)) {
			case ImapStringType.Literal:
				var literal = Encoding.UTF8.GetBytes (value);
				var length = literal.Length.ToString ();
				buf = Encoding.ASCII.GetBytes (length);

				builder.WriteByte ((byte) '{');
				builder.Write (buf, 0, buf.Length);
				if (Engine.IsGMail || (Engine.Capabilities & ImapCapabilities.LiteralPlus) != 0)
					builder.WriteByte ((byte) '+');
				builder.WriteByte ((byte) '}');
				builder.WriteByte ((byte) '\r');
				builder.WriteByte ((byte) '\n');

				if (Engine.IsGMail || (Engine.Capabilities & ImapCapabilities.LiteralPlus) != 0) {
					builder.Write (literal, 0, literal.Length);
				} else {
					parts.Add (new ImapCommandPart (builder.ToArray (), new ImapLiteral (options, literal)));
					builder.SetLength (0);
				}
				break;
			case ImapStringType.QString:
				buf = Encoding.UTF8.GetBytes (MimeUtils.Quote (value));
				builder.Write (buf, 0, buf.Length);
				break;
			case ImapStringType.Atom:
				buf = Encoding.UTF8.GetBytes (value);
				builder.Write (buf, 0, buf.Length);
				break;
			}
		}

		/// <summary>
		/// Registers the untagged handler for the specified atom token.
		/// </summary>
		/// <param name="atom">The atom token.</param>
		/// <param name="handler">The handler.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <para><paramref name="atom"/> is <c>null</c>.</para>
		/// <para>-or-</para>
		/// <para><paramref name="handler"/> is <c>null</c>.</para>
		/// </exception>
		/// <exception cref="System.InvalidOperationException">
		/// Untagged handlers must be registered before the command has been queued.
		/// </exception>
		public void RegisterUntaggedHandler (string atom, ImapUntaggedHandler handler)
		{
			if (atom == null)
				throw new ArgumentNullException ("atom");

			if (handler == null)
				throw new ArgumentNullException ("handler");

			if (Status != ImapCommandStatus.Created)
				throw new InvalidOperationException ("Untagged handlers must be registered before the command has been queued.");

			UntaggedHandlers.Add (atom, handler);
		}

		/// <summary>
		/// Sends the next part of the command to the server.
		/// </summary>
		/// <exception cref="System.OperationCanceledException">
		/// The operation was canceled via the cancellation token.
		/// </exception>
		/// <exception cref="System.IO.IOException">
		/// An I/O error occurred.
		/// </exception>
		/// <exception cref="ImapProtocolException">
		/// An IMAP protocol error occurred.
		/// </exception>
		public bool Step ()
		{
			var supportsLiteralPlus = (Engine.Capabilities & ImapCapabilities.LiteralPlus) != 0;
			var idle = UserData as ImapIdleContext;
			var result = ImapCommandResult.None;
			ImapToken token;

			if (current == 0) {
				Tag = string.Format ("{0}{1:D8}", Engine.TagPrefix, Engine.Tag++);

				var buf = Encoding.ASCII.GetBytes (Tag + " ");
				Engine.Stream.Write (buf, 0, buf.Length, CancellationToken);
			}

			do {
				var command = parts[current].Command;

				Engine.Stream.Write (command, 0, command.Length, CancellationToken);

				if (!supportsLiteralPlus || parts[current].Literal == null)
					break;

				parts[current].Literal.WriteTo (Engine.Stream, CancellationToken);

				if (current + 1 >= parts.Count)
					break;

				current++;
			} while (true);

			Engine.Stream.Flush ();

			// now we need to read the response...
			do {
				if (Engine.State == ImapEngineState.Idle) {
					int timeout = Engine.Stream.ReadTimeout;

					try {
						Engine.Stream.ReadTimeout = -1;

						token = Engine.ReadToken (idle.LinkedToken);
						Engine.Stream.ReadTimeout = timeout;
					} catch (OperationCanceledException) {
						Engine.Stream.ReadTimeout = timeout;

						if (idle.IsCancellationRequested)
							throw;

						Engine.Stream.IsConnected = true;

						token = Engine.ReadToken (CancellationToken);
					}
				} else {
					token = Engine.ReadToken (CancellationToken);
				}

				if (token.Type == ImapTokenType.Plus) {
					// we've gotten a continuation response from the server
					var text = Engine.ReadLine (CancellationToken).Trim ();

					// if we've got a Literal pending, the '+' means we can send it now...
					if (!supportsLiteralPlus && parts[current].Literal != null) {
						parts[current].Literal.WriteTo (Engine.Stream, CancellationToken);
						break;
					}

					Debug.Assert (ContinuationHandler != null, "The ImapCommand's ContinuationHandler is null");

					ContinuationHandler (Engine, this, text);
				} else if (token.Type == ImapTokenType.Asterisk) {
					// we got an untagged response, let the engine handle this...
					Engine.ProcessUntaggedResponse (CancellationToken);
				} else if (token.Type == ImapTokenType.Atom && (string) token.Value == Tag) {
					// the next token should be "OK", "NO", or "BAD"
					token = Engine.ReadToken (CancellationToken);

					if (token.Type == ImapTokenType.Atom) {
						string atom = (string) token.Value;

						switch (atom) {
						case "BAD": result = ImapCommandResult.Bad; break;
						case "OK": result = ImapCommandResult.Ok; break;
						case "NO": result = ImapCommandResult.No; break;
						}

						if (result == ImapCommandResult.None)
							throw ImapEngine.UnexpectedToken (token, false);

						token = Engine.ReadToken (CancellationToken);
						if (token.Type == ImapTokenType.OpenBracket) {
							var code = Engine.ParseResponseCode (CancellationToken);
							RespCodes.Add (code);
							break;
						}

						if (token.Type != ImapTokenType.Eoln) {
							// consume the rest of the line...
							Engine.ReadLine (CancellationToken);
							break;
						}
					} else {
						// looks like we didn't get an "OK", "NO", or "BAD"...
						throw ImapEngine.UnexpectedToken (token, false);
					}
				} else if (token.Type == ImapTokenType.OpenBracket) {
					// Note: this is a work-around for broken IMAP servers like Office365.com that
					// return RESP-CODES that are not preceded by "* OK " such as the example in
					// issue #115 (https://github.com/jstedfast/MailKit/issues/115).
					var code = Engine.ParseResponseCode (CancellationToken);
					RespCodes.Add (code);
				} else {
					// no clue what we got...
					throw ImapEngine.UnexpectedToken (token, false);
				}
			} while (true);

			// the status should always be Active at this point, but just to be sure...
			if (Status == ImapCommandStatus.Active) {
				current++;

				if (current >= parts.Count || result != ImapCommandResult.None) {
					Status = ImapCommandStatus.Complete;
					Result = result;
					return false;
				}
			}

			return true;
		}
	}
}
