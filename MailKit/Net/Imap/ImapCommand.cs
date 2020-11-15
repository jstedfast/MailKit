//
// ImapCommand.cs
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
using System.Text;
using System.Threading;
using System.Globalization;
using System.Threading.Tasks;
using System.Collections.Generic;

using MimeKit;
using MimeKit.IO;
using MimeKit.Utils;

using SslStream = MailKit.Net.SslStream;
using NetworkStream = MailKit.Net.NetworkStream;

namespace MailKit.Net.Imap {
	/// <summary>
	/// An IMAP continuation handler.
	/// </summary>
	/// <remarks>
	/// All exceptions thrown by the handler are considered fatal and will
	/// force-disconnect the connection. If a non-fatal error occurs, set
	/// it on the <see cref="ImapCommand.Exception"/> property.
	/// </remarks>
	delegate Task ImapContinuationHandler (ImapEngine engine, ImapCommand ic, string text, bool doAsync);

	/// <summary>
	/// An IMAP untagged response handler.
	/// </summary>
	/// <remarks>
	/// <para>Most IMAP commands return their results in untagged responses.</para>
	/// </remarks>
	delegate Task ImapUntaggedHandler (ImapEngine engine, ImapCommand ic, int index, bool doAsync);

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

	enum ImapLiteralType {
		String,
		//Stream,
		MimeMessage
	}

	enum ImapStringType {
		Atom,
		QString,
		Literal,
		Nil
	}

	/// <summary>
	/// An IMAP IDLE context.
	/// </summary>
	/// <remarks>
	/// <para>An IMAP IDLE command does not work like normal commands. Unlike most commands,
	/// the IDLE command does not end until the client sends a separate "DONE" command.</para>
	/// <para>In order to facilitate this, the way this works is that the consumer of MailKit's
	/// IMAP APIs provides a 'doneToken' which signals to the command-processing loop to
	/// send the "DONE" command. Since, like every other IMAP command, it is also necessary to
	/// provide a means of cancelling the IDLE command, it becomes necessary to link the
	/// 'doneToken' and the 'cancellationToken' together.</para>
	/// </remarks>
	sealed class ImapIdleContext : IDisposable
	{
		static readonly byte[] DoneCommand = Encoding.ASCII.GetBytes ("DONE\r\n");
		CancellationTokenRegistration registration;

		/// <summary>
		/// Initializes a new instance of the <see cref="MailKit.Net.Imap.ImapIdleContext"/> class.
		/// </summary>
		/// <remarks>
		/// Creates a new <see cref="MailKit.Net.Imap.ImapIdleContext"/>.
		/// </remarks>
		/// <param name="engine">The IMAP engine.</param>
		/// <param name="doneToken">The done token.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		public ImapIdleContext (ImapEngine engine, CancellationToken doneToken, CancellationToken cancellationToken)
		{
			CancellationToken = cancellationToken;
			DoneToken = doneToken;
			Engine = engine;
		}

		/// <summary>
		/// Get the engine.
		/// </summary>
		/// <remarks>
		/// Gets the engine.
		/// </remarks>
		/// <value>The engine.</value>
		public ImapEngine Engine {
			get; private set;
		}

		/// <summary>
		/// Get the cancellation token.
		/// </summary>
		/// <remarks>
		/// Get the cancellation token.
		/// </remarks>
		/// <value>The cancellation token.</value>
		public CancellationToken CancellationToken {
			get; private set;
		}

		/// <summary>
		/// Get the done token.
		/// </summary>
		/// <remarks>
		/// Gets the done token.
		/// </remarks>
		/// <value>The done token.</value>
		public CancellationToken DoneToken {
			get; private set;
		}

#if false
		/// <summary>
		/// Get whether or not cancellation has been requested.
		/// </summary>
		/// <remarks>
		/// Gets whether or not cancellation has been requested.
		/// </remarks>
		/// <value><c>true</c> if cancellation has been requested; otherwise, <c>false</c>.</value>
		public bool IsCancellationRequested {
			get { return CancellationToken.IsCancellationRequested; }
		}

		/// <summary>
		/// Get whether or not the IDLE command should be ended.
		/// </summary>
		/// <remarks>
		/// Gets whether or not the IDLE command should be ended.
		/// </remarks>
		/// <value><c>true</c> if the IDLE command should end; otherwise, <c>false</c>.</value>
		public bool IsDoneRequested {
			get { return DoneToken.IsCancellationRequested; }
		}
#endif

		void IdleComplete ()
		{
			if (Engine.State == ImapEngineState.Idle) {
				try {
					Engine.Stream.Write (DoneCommand, 0, DoneCommand.Length, CancellationToken);
					Engine.Stream.Flush (CancellationToken);
				} catch {
					return;
				}

				Engine.State = ImapEngineState.Selected;
			}
		}

		/// <summary>
		/// Callback method to be used as the ImapCommand's ContinuationHandler.
		/// </summary>
		/// <remarks>
		/// Callback method to be used as the ImapCommand's ContinuationHandler.
		/// </remarks>
		/// <param name="engine">The ImapEngine.</param>
		/// <param name="ic">The ImapCommand.</param>
		/// <param name="text">The text.</param>
		/// <param name="doAsync"><c>true</c> if the command is being run asynchronously; otherwise, <c>false</c>.</param>
		/// <returns></returns>
		public Task ContinuationHandler (ImapEngine engine, ImapCommand ic, string text, bool doAsync)
		{
			Engine.State = ImapEngineState.Idle;

			registration = DoneToken.Register (IdleComplete);

			return Task.FromResult (true);
		}

		/// <summary>
		/// Releases all resource used by the <see cref="MailKit.Net.Imap.ImapIdleContext"/> object.
		/// </summary>
		/// <remarks>Call <see cref="Dispose"/> when you are finished using the <see cref="MailKit.Net.Imap.ImapIdleContext"/>. The
		/// <see cref="Dispose"/> method leaves the <see cref="MailKit.Net.Imap.ImapIdleContext"/> in an unusable state. After
		/// calling <see cref="Dispose"/>, you must release all references to the
		/// <see cref="MailKit.Net.Imap.ImapIdleContext"/> so the garbage collector can reclaim the memory that the
		/// <see cref="MailKit.Net.Imap.ImapIdleContext"/> was occupying.</remarks>
		public void Dispose ()
		{
			registration.Dispose ();
		}
	}

	/// <summary>
	/// An IMAP literal object.
	/// </summary>
	/// <remarks>
	/// The literal can be a string, byte[], Stream, or a MimeMessage.
	/// </remarks>
	class ImapLiteral
	{
		public readonly ImapLiteralType Type;
		public readonly object Literal;
		readonly FormatOptions format;
		readonly Action<int> update;

		/// <summary>
		/// Initializes a new instance of the <see cref="MailKit.Net.Imap.ImapLiteral"/> class.
		/// </summary>
		/// <remarks>
		/// Creates a new <see cref="MailKit.Net.Imap.ImapLiteral"/>.
		/// </remarks>
		/// <param name="options">The formatting options.</param>
		/// <param name="message">The message.</param>
		/// <param name="action">The progress update action.</param>
		public ImapLiteral (FormatOptions options, MimeMessage message, Action<int> action = null)
		{
			format = options.Clone ();
			format.NewLineFormat = NewLineFormat.Dos;

			update = action;

			Type = ImapLiteralType.MimeMessage;
			Literal = message;
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="MailKit.Net.Imap.ImapLiteral"/> class.
		/// </summary>
		/// <remarks>
		/// Creates a new <see cref="MailKit.Net.Imap.ImapLiteral"/>.
		/// </remarks>
		/// <param name="options">The formatting options.</param>
		/// <param name="literal">The literal.</param>
		public ImapLiteral (FormatOptions options, byte[] literal)
		{
			format = options.Clone ();
			format.NewLineFormat = NewLineFormat.Dos;

			Type = ImapLiteralType.String;
			Literal = literal;
		}

		/// <summary>
		/// Get the length of the literal, in bytes.
		/// </summary>
		/// <remarks>
		/// Gets the length of the literal, in bytes.
		/// </remarks>
		/// <value>The length.</value>
		public long Length {
			get {
				if (Type == ImapLiteralType.String)
					return ((byte[]) Literal).Length;

				using (var measure = new MeasuringStream ()) {
					//if (Type == ImapLiteralType.Stream) {
					//	var stream = (Stream) Literal;
					//	stream.CopyTo (measure, 4096);
					//	stream.Position = 0;

					//	return measure.Length;
					//}

					((MimeMessage) Literal).WriteTo (format, measure);

					return measure.Length;
				}
			}
		}

		/// <summary>
		/// Write the literal to the specified stream.
		/// </summary>
		/// <remarks>
		/// Writes the literal to the specified stream.
		/// </remarks>
		/// <param name="stream">The stream.</param>
		/// <param name="doAsync">Whether the literal should be written asynchronously or not.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		public async Task WriteToAsync (ImapStream stream, bool doAsync, CancellationToken cancellationToken)
		{
			if (Type == ImapLiteralType.String) {
				var bytes = (byte[]) Literal;

				if (doAsync) {
					await stream.WriteAsync (bytes, 0, bytes.Length, cancellationToken).ConfigureAwait (false);
					await stream.FlushAsync (cancellationToken).ConfigureAwait (false);
				} else {
					stream.Write (bytes, 0, bytes.Length, cancellationToken);
					stream.Flush (cancellationToken);
				}
				return;
			}

			//if (Type == ImapLiteralType.Stream) {
			//	var literal = (Stream) Literal;
			//	var buf = new byte[4096];
			//	int nread;

			//	if (doAsync) {
			//		while ((nread = await literal.ReadAsync (buf, 0, buf.Length, cancellationToken).ConfigureAwait (false)) > 0)
			//			await stream.WriteAsync (buf, 0, nread, cancellationToken).ConfigureAwait (false);

			//		await stream.FlushAsync (cancellationToken).ConfigureAwait (false);
			//	} else {
			//		while ((nread = literal.Read (buf, 0, buf.Length)) > 0)
			//			stream.Write (buf, 0, nread, cancellationToken);

			//		stream.Flush (cancellationToken);
			//	}
			//	return;
			//}

			var message = (MimeMessage) Literal;

			using (var s = new ProgressStream (stream, update)) {
				if (doAsync) {
					await message.WriteToAsync (format, s, cancellationToken).ConfigureAwait (false);
					await s.FlushAsync (cancellationToken).ConfigureAwait (false);
				} else {
					message.WriteTo (format, s, cancellationToken);
					s.Flush (cancellationToken);
				}
			}
		}
	}

	/// <summary>
	/// A partial IMAP command.
	/// </summary>
	/// <remarks>
	/// IMAP commands that contain literal strings are broken up into multiple parts
	/// in case the IMAP server does not support the LITERAL+ extension. These parts
	/// are then sent individually as we receive "+" responses from the server.
	/// </remarks>
	class ImapCommandPart
	{
		public readonly byte[] Command;
		public readonly ImapLiteral Literal;
		public readonly bool WaitForContinuation;

		public ImapCommandPart (byte[] command, ImapLiteral literal, bool wait = true)
		{
			WaitForContinuation = wait;
			Command = command;
			Literal = literal;
		}
	}

	/// <summary>
	/// An IMAP command.
	/// </summary>
	class ImapCommand
	{
		static readonly byte[] UTF8LiteralTokenPrefix = Encoding.ASCII.GetBytes ("UTF8 (~{");
		static readonly byte[] LiteralTokenSuffix = { (byte) '}', (byte) '\r', (byte) '\n' };
		static readonly byte[] Nil = { (byte) 'N', (byte) 'I', (byte) 'L' };
		static readonly byte[] NewLine = { (byte) '\r', (byte) '\n' };
		static readonly byte[] LiteralTokenPrefix = { (byte) '{' };

		public Dictionary<string, ImapUntaggedHandler> UntaggedHandlers { get; private set; }
		public ImapContinuationHandler ContinuationHandler { get; set; }
		public CancellationToken CancellationToken { get; private set; }
		public ImapCommandStatus Status { get; internal set; }
		public ImapCommandResponse Response { get; internal set; }
		public ITransferProgress Progress { get; internal set; }
		public Exception Exception { get; internal set; }
		public readonly List<ImapResponseCode> RespCodes;
		public string ResponseText { get; internal set; }
		public ImapFolder Folder { get; private set; }
		public object UserData { get; internal set; }
		public bool ListReturnsSubscribed { get; internal set; }
		public bool Logout { get; private set; }
		public bool Lsub { get; internal set; }
		public string Tag { get; private set; }
		public bool Bye { get; internal set; }

		readonly List<ImapCommandPart> parts = new List<ImapCommandPart> ();
		readonly ImapEngine Engine;
		long totalSize, nwritten;
		int current;

		/// <summary>
		/// Initializes a new instance of the <see cref="MailKit.Net.Imap.ImapCommand"/> class.
		/// </summary>
		/// <remarks>
		/// Creates a new <see cref="MailKit.Net.Imap.ImapCommand"/>.
		/// </remarks>
		/// <param name="engine">The IMAP engine that will be sending the command.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <param name="folder">The IMAP folder that the command operates on.</param>
		/// <param name="options">The formatting options.</param>
		/// <param name="format">The command format.</param>
		/// <param name="args">The command arguments.</param>
		public ImapCommand (ImapEngine engine, CancellationToken cancellationToken, ImapFolder folder, FormatOptions options, string format, params object[] args)
		{
			UntaggedHandlers = new Dictionary<string, ImapUntaggedHandler> (StringComparer.OrdinalIgnoreCase);
			Logout = format.Equals ("LOGOUT\r\n", StringComparison.Ordinal);
			RespCodes = new List<ImapResponseCode> ();
			CancellationToken = cancellationToken;
			Response = ImapCommandResponse.None;
			Status = ImapCommandStatus.Created;
			Engine = engine;
			Folder = folder;

			using (var builder = new MemoryStream ()) {
				byte[] buf, utf8 = new byte[8];
				int argc = 0;
				string str;

				for (int i = 0; i < format.Length; i++) {
					if (format[i] == '%') {
						switch (format[++i]) {
						case '%': // a literal %
							builder.WriteByte ((byte) '%');
							break;
						case 'd': // an integer
							str = ((int) args[argc++]).ToString (CultureInfo.InvariantCulture);
							buf = Encoding.ASCII.GetBytes (str);
							builder.Write (buf, 0, buf.Length);
							break;
						case 'u': // an unsigned integer
							str = ((uint) args[argc++]).ToString (CultureInfo.InvariantCulture);
							buf = Encoding.ASCII.GetBytes (str);
							builder.Write (buf, 0, buf.Length);
							break;
						case 's':
							str = (string) args[argc++];
							buf = Encoding.ASCII.GetBytes (str);
							builder.Write (buf, 0, buf.Length);
							break;
						case 'F': // an ImapFolder
							var utf7 = ((ImapFolder) args[argc++]).EncodedName;
							AppendString (options, true, builder, utf7);
							break;
						case 'L': // a MimeMessage or a byte[]
							var arg = args[argc++];
							ImapLiteral literal;
							byte[] prefix;

							if (arg is MimeMessage message) {
								prefix = options.International ? UTF8LiteralTokenPrefix : LiteralTokenPrefix;
								literal = new ImapLiteral (options, message, UpdateProgress);
							} else {
								literal = new ImapLiteral (options, (byte[]) arg);
								prefix = LiteralTokenPrefix;
							}

							var length = literal.Length;
							bool wait = true;

							builder.Write (prefix, 0, prefix.Length);
							buf = Encoding.ASCII.GetBytes (length.ToString (CultureInfo.InvariantCulture));
							builder.Write (buf, 0, buf.Length);

							if (CanUseNonSynchronizedLiteral (Engine, length)) {
								builder.WriteByte ((byte) '+');
								wait = false;
							}

							builder.Write (LiteralTokenSuffix, 0, LiteralTokenSuffix.Length);

							totalSize += length;

							parts.Add (new ImapCommandPart (builder.ToArray (), literal, wait));
							builder.SetLength (0);

							if (prefix == UTF8LiteralTokenPrefix)
								builder.WriteByte ((byte) ')');
							break;
						case 'S': // a string which may need to be quoted or made into a literal
							AppendString (options, true, builder, (string) args[argc++]);
							break;
						case 'Q': // similar to %S but string must be quoted at a minimum
							AppendString (options, false, builder, (string) args[argc++]);
							break;
						default:
							throw new FormatException ();
						}
					} else if (format[i] < 128) {
						builder.WriteByte ((byte) format[i]);
					} else {
						int nchars = char.IsSurrogate (format[i]) ? 2 : 1;
						int nbytes = Encoding.UTF8.GetBytes (format, i, nchars, utf8, 0);
						builder.Write (utf8, 0, nbytes);
						i += nchars - 1;
					}
				}

				parts.Add (new ImapCommandPart (builder.ToArray (), null));
			}
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="MailKit.Net.Imap.ImapCommand"/> class.
		/// </summary>
		/// <remarks>
		/// Creates a new <see cref="MailKit.Net.Imap.ImapCommand"/>.
		/// </remarks>
		/// <param name="engine">The IMAP engine that will be sending the command.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <param name="folder">The IMAP folder that the command operates on.</param>
		/// <param name="format">The command format.</param>
		/// <param name="args">The command arguments.</param>
		public ImapCommand (ImapEngine engine, CancellationToken cancellationToken, ImapFolder folder, string format, params object[] args)
			: this (engine, cancellationToken, folder, FormatOptions.Default, format, args)
		{
		}

		internal static int EstimateCommandLength (ImapEngine engine, FormatOptions options, string format, params object[] args)
		{
			const int EstimatedTagLength = 10;
			var eoln = false;
			int length = 0;
			int argc = 0;
			string str;

			for (int i = 0; i < format.Length; i++) {
				if (format[i] == '%') {
					switch (format[++i]) {
					case '%': // a literal %
						length++;
						break;
					case 'd': // an integer
						str = ((int) args[argc++]).ToString (CultureInfo.InvariantCulture);
						length += str.Length;
						break;
					case 'u': // an unsigned integer
						str = ((uint) args[argc++]).ToString (CultureInfo.InvariantCulture);
						length += str.Length;
						break;
					case 's':
						str = (string) args[argc++];
						length += str.Length;
						break;
					case 'F': // an ImapFolder
						var utf7 = ((ImapFolder) args[argc++]).EncodedName;
						length += EstimateStringLength (engine, true, utf7, out eoln);
						break;
					case 'L': // a MimeMessage or a byte[]
						// Note: This is commented out because %L is only ever used for APPEND and REPLACE commands which
						// never need to split the command to keep it under the max line length.
						//var arg = args[argc++];
						//byte[] prefix;
						//long len;

						//if (arg is MimeMessage message) {
						//	prefix = options.International ? UTF8LiteralTokenPrefix : LiteralTokenPrefix;
						//	var literal = new ImapLiteral (options, message, null);
						//	len = literal.Length;
						//} else {
						//	len = ((byte[]) arg).Length;
						//	prefix = LiteralTokenPrefix;
						//}

						//length += prefix.Length;
						//length += Encoding.ASCII.GetByteCount (len.ToString (CultureInfo.InvariantCulture));

						//if (CanUseNonSynchronizedLiteral (engine, len))
						//	length++;

						//length += LiteralTokenSuffix.Length;

						//if (prefix == UTF8LiteralTokenPrefix)
						//	length++;

						//eoln = true;
						break;
					case 'S': // a string which may need to be quoted or made into a literal
						length += EstimateStringLength (engine, true, (string) args[argc++], out eoln);
						break;
					case 'Q': // similar to %S but string must be quoted at a minimum
						length += EstimateStringLength (engine, false, (string) args[argc++], out eoln);
						break;
					default:
						throw new FormatException ();
					}

					if (eoln)
						break;
				} else {
					length++;
				}
			}

			return length + EstimatedTagLength;
		}

		internal static int EstimateCommandLength (ImapEngine engine, string format, params object[] args)
		{
			return EstimateCommandLength (engine, FormatOptions.Default, format, args);
		}

		void UpdateProgress (int n)
		{
			nwritten += n;

			if (Progress != null)
				Progress.Report (nwritten, totalSize);
		}

		static bool IsAtom (char c)
		{
			return c < 128 && !char.IsControl (c) && "(){ \t%*\\\"]".IndexOf (c) == -1;
		}

		static bool IsQuotedSafe (ImapEngine engine, char c)
		{
			return (c < 128 || engine.UTF8Enabled) && !char.IsControl (c);
		}

		internal static ImapStringType GetStringType (ImapEngine engine, string value, bool allowAtom)
		{
			var type = allowAtom ? ImapStringType.Atom : ImapStringType.QString;

			if (value == null)
				return ImapStringType.Nil;

			if (value.Length == 0)
				return ImapStringType.QString;

			for (int i = 0; i < value.Length; i++) {
				if (!IsAtom (value[i])) {
					if (!IsQuotedSafe (engine, value[i]))
						return ImapStringType.Literal;

					type = ImapStringType.QString;
				}
			}

			return type;
		}

		static bool CanUseNonSynchronizedLiteral (ImapEngine engine, long length)
		{
			return (engine.Capabilities & ImapCapabilities.LiteralPlus) != 0 ||
				(length <= 4096 && (engine.Capabilities & ImapCapabilities.LiteralMinus) != 0);
		}

		static int EstimateStringLength (ImapEngine engine, bool allowAtom, string value, out bool eoln)
		{
			eoln = false;

			switch (GetStringType (engine, value, allowAtom)) {
			case ImapStringType.Literal:
				var literal = Encoding.UTF8.GetByteCount (value);
				var plus = CanUseNonSynchronizedLiteral (engine, literal);
				int length = "{}\r\n".Length;

				length += literal.ToString (CultureInfo.InvariantCulture).Length;
				if (plus)
					length++;

				eoln = true;

				return length++;
			case ImapStringType.QString:
				return Encoding.UTF8.GetByteCount (MimeUtils.Quote (value));
			case ImapStringType.Nil:
				return Nil.Length;
			default:
				return value.Length;
			}
		}

		void AppendString (FormatOptions options, bool allowAtom, MemoryStream builder, string value)
		{
			byte[] buf;

			switch (GetStringType (Engine, value, allowAtom)) {
			case ImapStringType.Literal:
				var literal = Encoding.UTF8.GetBytes (value);
				var plus = CanUseNonSynchronizedLiteral (Engine, literal.Length);
				var length = literal.Length.ToString (CultureInfo.InvariantCulture);
				buf = Encoding.ASCII.GetBytes (length);

				builder.WriteByte ((byte) '{');
				builder.Write (buf, 0, buf.Length);
				if (plus)
					builder.WriteByte ((byte) '+');
				builder.WriteByte ((byte) '}');
				builder.WriteByte ((byte) '\r');
				builder.WriteByte ((byte) '\n');

				if (plus) {
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
			case ImapStringType.Nil:
				builder.Write (Nil, 0, Nil.Length);
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
				throw new ArgumentNullException (nameof (atom));

			if (handler == null)
				throw new ArgumentNullException (nameof (handler));

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
		public async Task<bool> StepAsync (bool doAsync)
		{
			var supportsLiteralPlus = (Engine.Capabilities & ImapCapabilities.LiteralPlus) != 0;
			var idle = UserData as ImapIdleContext;
			var result = ImapCommandResponse.None;
			ImapToken token;

			// construct and write the command tag if this is the initial state
			if (current == 0) {
				Tag = string.Format (CultureInfo.InvariantCulture, "{0}{1:D8}", Engine.TagPrefix, Engine.Tag++);

				var buf = Encoding.ASCII.GetBytes (Tag + " ");

				if (doAsync)
					await Engine.Stream.WriteAsync (buf, 0, buf.Length, CancellationToken).ConfigureAwait (false);
				else
					Engine.Stream.Write (buf, 0, buf.Length, CancellationToken);
			}

			do {
				var command = parts[current].Command;

				if (doAsync)
					await Engine.Stream.WriteAsync (command, 0, command.Length, CancellationToken).ConfigureAwait (false);
				else
					Engine.Stream.Write (command, 0, command.Length, CancellationToken);

				// if the server doesn't support LITERAL+ (or LITERAL-), we'll need to wait
				// for a "+" response before writing out the any literals...
				if (parts[current].WaitForContinuation)
					break;

				// otherwise, we can write out any and all literal tokens we have...
				await parts[current].Literal.WriteToAsync (Engine.Stream, doAsync, CancellationToken).ConfigureAwait (false);

				if (current + 1 >= parts.Count)
					break;

				current++;
			} while (true);

			if (doAsync)
				await Engine.Stream.FlushAsync (CancellationToken).ConfigureAwait (false);
			else
				Engine.Stream.Flush (CancellationToken);

			// now we need to read the response...
			do {
				if (Engine.State == ImapEngineState.Idle) {
					int timeout = Timeout.Infinite;

					if (Engine.Stream.CanTimeout) {
						timeout = Engine.Stream.ReadTimeout;
						Engine.Stream.ReadTimeout = Timeout.Infinite;
					}

					try {
						token = await Engine.ReadTokenAsync (doAsync, CancellationToken).ConfigureAwait (false);
					} finally {
						if (Engine.Stream != null && Engine.Stream.IsConnected && Engine.Stream.CanTimeout)
							Engine.Stream.ReadTimeout = timeout;
					}
				} else {
					token = await Engine.ReadTokenAsync (doAsync, CancellationToken).ConfigureAwait (false);
				}

				if (token.Type == ImapTokenType.Atom && token.Value.ToString () == "+") {
					// we've gotten a continuation response from the server
					var text = (await Engine.ReadLineAsync (doAsync, CancellationToken).ConfigureAwait (false)).Trim ();

					// if we've got a Literal pending, the '+' means we can send it now...
					if (!supportsLiteralPlus && parts[current].Literal != null) {
						await parts[current].Literal.WriteToAsync (Engine.Stream, doAsync, CancellationToken).ConfigureAwait (false);
						break;
					}

					if (ContinuationHandler != null) {
						await ContinuationHandler (Engine, this, text, doAsync).ConfigureAwait (false);
					} else if (doAsync) {
						await Engine.Stream.WriteAsync (NewLine, 0, NewLine.Length, CancellationToken).ConfigureAwait (false);
						await Engine.Stream.FlushAsync (CancellationToken).ConfigureAwait (false);
					} else {
						Engine.Stream.Write (NewLine, 0, NewLine.Length, CancellationToken);
						Engine.Stream.Flush (CancellationToken);
					}
				} else if (token.Type == ImapTokenType.Asterisk) {
					// we got an untagged response, let the engine handle this...
					await Engine.ProcessUntaggedResponseAsync (doAsync, CancellationToken).ConfigureAwait (false);
				} else if (token.Type == ImapTokenType.Atom && (string) token.Value == Tag) {
					// the next token should be "OK", "NO", or "BAD"
					token = await Engine.ReadTokenAsync (doAsync, CancellationToken).ConfigureAwait (false);

					ImapEngine.AssertToken (token, ImapTokenType.Atom, "Syntax error in tagged response. {0}", token);

					string atom = (string) token.Value;

					switch (atom) {
					case "BAD": result = ImapCommandResponse.Bad; break;
					case "OK": result = ImapCommandResponse.Ok; break;
					case "NO": result = ImapCommandResponse.No; break;
					default: throw ImapEngine.UnexpectedToken ("Syntax error in tagged response. {0}", token);
					}

					token = await Engine.ReadTokenAsync (doAsync, CancellationToken).ConfigureAwait (false);
					if (token.Type == ImapTokenType.OpenBracket) {
						var code = await Engine.ParseResponseCodeAsync (true, doAsync, CancellationToken).ConfigureAwait (false);
						RespCodes.Add (code);
						break;
					}

					if (token.Type != ImapTokenType.Eoln) {
						// consume the rest of the line...
						var line = await Engine.ReadLineAsync (doAsync, CancellationToken).ConfigureAwait (false);
						ResponseText = (((string) token.Value) + line).TrimEnd ();
						break;
					}
				} else if (token.Type == ImapTokenType.OpenBracket) {
					// Note: this is a work-around for broken IMAP servers like Office365.com that
					// return RESP-CODES that are not preceded by "* OK " such as the example in
					// issue #115 (https://github.com/jstedfast/MailKit/issues/115).
					var code = await Engine.ParseResponseCodeAsync (false, doAsync, CancellationToken).ConfigureAwait (false);
					RespCodes.Add (code);
				} else {
					// no clue what we got...
					throw ImapEngine.UnexpectedToken ("Syntax error in response. Unexpected token: {0}", token);
				}
			} while (Status == ImapCommandStatus.Active);

			if (Status == ImapCommandStatus.Active) {
				current++;

				if (current >= parts.Count || result != ImapCommandResponse.None) {
					Status = ImapCommandStatus.Complete;
					Response = result;
					return false;
				}

				return true;
			}

			return false;
		}

		/// <summary>
		/// Get the first response-code of the specified type.
		/// </summary>
		/// <remarks>
		/// Gets the first response-code of the specified type.
		/// </remarks>
		/// <param name="type">The type of response-code.</param>
		/// <returns>The response-code if it exists; otherwise, <c>null</c>.</returns>
		public ImapResponseCode GetResponseCode (ImapResponseCodeType type)
		{
			for (int i = 0; i < RespCodes.Count; i++) {
				if (RespCodes[i].Type == type)
					return RespCodes[i];
			}

			return null;
		}
	}
}
