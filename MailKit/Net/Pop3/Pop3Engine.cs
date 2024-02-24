//
// Pop3Engine.cs
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
using System.Text;
using System.Threading;
using System.Diagnostics;
using System.Globalization;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace MailKit.Net.Pop3 {
	/// <summary>
	/// The state of the <see cref="Pop3Engine"/>.
	/// </summary>
	enum Pop3EngineState {
		/// <summary>
		/// The Pop3Engine is in the disconnected state.
		/// </summary>
		Disconnected,

		/// <summary>
		/// The Pop3Engine is in the connected state.
		/// </summary>
		Connected,

		/// <summary>
		/// The Pop3Engine is in the transaction state, indicating that it is 
		/// authenticated and may retrieve messages from the server.
		/// </summary>
		Transaction
	}

	/// <summary>
	/// A POP3 command engine.
	/// </summary>
	class Pop3Engine
	{
#if NET6_0_OR_GREATER
		readonly ClientMetrics metrics;
#endif
		readonly List<Pop3Command> queue;
		long clientConnectedTimestamp;
		Pop3Stream stream;

		/// <summary>
		/// Initializes a new instance of the <see cref="MailKit.Net.Pop3.Pop3Engine"/> class.
		/// </summary>
		public Pop3Engine ()
		{
			AuthenticationMechanisms = new HashSet<string> (StringComparer.Ordinal);
			Capabilities = Pop3Capabilities.User;
			queue = new List<Pop3Command> ();

#if NET6_0_OR_GREATER
			// Use the globally configured Pop3Client metrics.
			metrics = Telemetry.Pop3Client.Metrics;
#endif
		}

		/// <summary>
		/// Gets the URI of the POP3 server.
		/// </summary>
		/// <remarks>
		/// Gets the URI of the POP3 server.
		/// </remarks>
		/// <value>The URI of the POP3 server.</value>
		public Uri Uri {
			get; internal set;
		}

		/// <summary>
		/// Gets the authentication mechanisms supported by the POP3 server.
		/// </summary>
		/// <remarks>
		/// The authentication mechanisms are queried durring the
		/// <see cref="ConnectAsync(Pop3Stream,CancellationToken)"/> method.
		/// </remarks>
		/// <value>The authentication mechanisms.</value>
		public HashSet<string> AuthenticationMechanisms {
			get; private set;
		}

		/// <summary>
		/// Gets the capabilities supported by the POP3 server.
		/// </summary>
		/// <remarks>
		/// The capabilities will not be known until a successful connection
		/// has been made via the <see cref="ConnectAsync(Pop3Stream,CancellationToken)"/> method.
		/// </remarks>
		/// <value>The capabilities.</value>
		public Pop3Capabilities Capabilities {
			get; set;
		}

		/// <summary>
		/// Gets the underlying POP3 stream.
		/// </summary>
		/// <remarks>
		/// Gets the underlying POP3 stream.
		/// </remarks>
		/// <value>The pop3 stream.</value>
		public Pop3Stream Stream {
			get { return stream; }
		}

		/// <summary>
		/// Gets or sets the state of the engine.
		/// </summary>
		/// <remarks>
		/// Gets or sets the state of the engine.
		/// </remarks>
		/// <value>The engine state.</value>
		public Pop3EngineState State {
			get; internal set;
		}

		/// <summary>
		/// Gets whether or not the engine is currently connected to a POP3 server.
		/// </summary>
		/// <remarks>
		/// Gets whether or not the engine is currently connected to a POP3 server.
		/// </remarks>
		/// <value><c>true</c> if the engine is connected; otherwise, <c>false</c>.</value>
		public bool IsConnected {
			get { return stream != null && stream.IsConnected; }
		}

		/// <summary>
		/// Gets the APOP authentication token.
		/// </summary>
		/// <remarks>
		/// Gets the APOP authentication token.
		/// </remarks>
		/// <value>The APOP authentication token.</value>
		public string ApopToken {
			get; private set;
		}

		/// <summary>
		/// Gets the EXPIRE extension policy value.
		/// </summary>
		/// <remarks>
		/// Gets the EXPIRE extension policy value.
		/// </remarks>
		/// <value>The EXPIRE policy.</value>
		public int ExpirePolicy {
			get; private set;
		}

		/// <summary>
		/// Gets the implementation details of the server.
		/// </summary>
		/// <remarks>
		/// Gets the implementation details of the server.
		/// </remarks>
		/// <value>The implementation details.</value>
		public string Implementation {
			get; private set;
		}

		/// <summary>
		/// Gets the login delay.
		/// </summary>
		/// <remarks>
		/// Gets the login delay.
		/// </remarks>
		/// <value>The login delay.</value>
		public int LoginDelay {
			get; private set;
		}

		void CheckConnected ()
		{
			if (stream == null)
				throw new InvalidOperationException ();
		}

		void Initialize (Pop3Stream pop3)
		{
			stream?.Dispose ();

			clientConnectedTimestamp = Stopwatch.GetTimestamp ();
			Capabilities = Pop3Capabilities.User;
			AuthenticationMechanisms.Clear ();
			State = Pop3EngineState.Disconnected;
			ApopToken = null;
			stream = pop3;
		}

		void ParseGreeting (string greeting)
		{
			int index = greeting.IndexOf (' ');
			string token, text;

			if (index != -1) {
				token = greeting.Substring (0, index);

				while (index < greeting.Length && char.IsWhiteSpace (greeting[index]))
					index++;

				if (index < greeting.Length)
					text = greeting.Substring (index);
				else
					text = string.Empty;
			} else {
				text = string.Empty;
				token = greeting;
			}

			if (token != "+OK") {
				stream.Dispose ();
				stream = null;

				throw new Pop3ProtocolException (string.Format ("Unexpected greeting from server: {0}", greeting));
			}

			index = text.IndexOf ('<');
			if (index != -1 && index + 1 < text.Length) {
				int endIndex = text.IndexOf ('>', index + 1);

				if (endIndex++ != -1) {
					ApopToken = text.Substring (index, endIndex - index);
					Capabilities |= Pop3Capabilities.Apop;
				}
			}

			State = Pop3EngineState.Connected;
		}

		public NetworkOperation StartNetworkOperation (string name)
		{
#if NET6_0_OR_GREATER
			return NetworkOperation.Start (name, Uri, Telemetry.Pop3Client.ActivitySource, metrics);
#else
			return NetworkOperation.Start (name, Uri);
#endif
		}

		/// <summary>
		/// Takes posession of the <see cref="Pop3Stream"/> and reads the greeting.
		/// </summary>
		/// <remarks>
		/// Takes posession of the <see cref="Pop3Stream"/> and reads the greeting.
		/// </remarks>
		/// <param name="pop3">The pop3 stream.</param>
		/// <param name="cancellationToken">The cancellation token</param>
		public void Connect (Pop3Stream pop3, CancellationToken cancellationToken)
		{
			Initialize (pop3);

			// read the pop3 server greeting
			var greeting = ReadLine (cancellationToken).TrimEnd ();

			ParseGreeting (greeting);
		}

		/// <summary>
		/// Takes posession of the <see cref="Pop3Stream"/> and reads the greeting.
		/// </summary>
		/// <remarks>
		/// Takes posession of the <see cref="Pop3Stream"/> and reads the greeting.
		/// </remarks>
		/// <param name="pop3">The pop3 stream.</param>
		/// <param name="cancellationToken">The cancellation token</param>
		public async Task ConnectAsync (Pop3Stream pop3, CancellationToken cancellationToken)
		{
			Initialize (pop3);

			// read the pop3 server greeting
			var greeting = (await ReadLineAsync (cancellationToken).ConfigureAwait (false)).TrimEnd ();

			ParseGreeting (greeting);
		}

		public event EventHandler<EventArgs> Disconnected;

		void OnDisconnected ()
		{
			Disconnected?.Invoke (this, EventArgs.Empty);
		}

		void RecordClientDisconnected (Exception ex)
		{
#if NET6_0_OR_GREATER
			metrics?.RecordClientDisconnected (clientConnectedTimestamp, Uri, ex);
#endif
			clientConnectedTimestamp = 0;
		}

		/// <summary>
		/// Disconnects the <see cref="Pop3Engine"/>.
		/// </summary>
		/// <remarks>
		/// Disconnects the <see cref="Pop3Engine"/>.
		/// </remarks>
		/// <param name="ex">The exception that is causing the disconnection.</param>
		public void Disconnect (Exception ex)
		{
			RecordClientDisconnected (ex);

			if (stream != null) {
				stream.Dispose ();
				stream = null;
			}

			if (State != Pop3EngineState.Disconnected) {
				State = Pop3EngineState.Disconnected;
				OnDisconnected ();
			}
		}

		/// <summary>
		/// Reads a single line from the <see cref="Pop3Stream"/>.
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
		/// An I/O pex occurred.
		/// </exception>
		public string ReadLine (CancellationToken cancellationToken)
		{
			CheckConnected ();

			using (var builder = new ByteArrayBuilder (64)) {
				bool complete;

				do {
					complete = stream.ReadLine (builder, cancellationToken);
				} while (!complete);

				// FIXME: All callers expect CRLF to be trimmed, but many also want all trailing whitespace trimmed.
				builder.TrimNewLine ();

				return builder.ToString ();
			}
		}

		/// <summary>
		/// Reads a single line from the <see cref="Pop3Stream"/>.
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
		/// An I/O pex occurred.
		/// </exception>
		public async Task<string> ReadLineAsync (CancellationToken cancellationToken)
		{
			CheckConnected ();

			using (var builder = new ByteArrayBuilder (64)) {
				bool complete;

				do {
					complete = await stream.ReadLineAsync (builder, cancellationToken).ConfigureAwait (false);
				} while (!complete);

				// FIXME: All callers expect CRLF to be trimmed, but many also want all trailing whitespace trimmed.
				builder.TrimNewLine ();

				return builder.ToString ();
			}
		}

		public static Pop3CommandStatus GetCommandStatus (string response, out string text)
		{
			int index = response.IndexOf (' ');
			string token;

			if (index != -1) {
				token = response.Substring (0, index);

				while (index < response.Length && char.IsWhiteSpace (response[index]))
					index++;

				if (index < response.Length)
					text = response.Substring (index);
				else
					text = string.Empty;
			} else {
				text = string.Empty;
				token = response;
			}

			if (token == "+OK")
				return Pop3CommandStatus.Ok;

			if (token == "-ERR")
				return Pop3CommandStatus.Error;

			if (token == "+")
				return Pop3CommandStatus.Continue;

			return Pop3CommandStatus.ProtocolError;
		}

		void ReadResponse (Pop3Command pc, CancellationToken cancellationToken)
		{
			string response;

			try {
				response = ReadLine (cancellationToken).TrimEnd ();
			} catch (Exception ex) {
				pc.Status = Pop3CommandStatus.ProtocolError;
				Disconnect (ex);
				throw;
			}

			pc.Status = GetCommandStatus (response, out string text);
			pc.StatusText = text;

			switch (pc.Status) {
			case Pop3CommandStatus.ProtocolError:
				var pex = new Pop3ProtocolException (string.Format ("Unexpected response from server: {0}", response));
				Disconnect (pex);
				throw pex;
			case Pop3CommandStatus.Continue:
			case Pop3CommandStatus.Ok:
				if (pc.Handler != null) {
					try {
						pc.Handler (this, pc, text, false, cancellationToken);
					} catch (Exception ex) {
						pc.Status = Pop3CommandStatus.ProtocolError;
						Disconnect (ex);
						throw;
					}
				}
				break;
			}
		}

		async Task ReadResponseAsync (Pop3Command pc, CancellationToken cancellationToken)
		{
			string response;

			try {
				response = (await ReadLineAsync (cancellationToken).ConfigureAwait (false)).TrimEnd ();
			} catch (Exception ex) {
				pc.Status = Pop3CommandStatus.ProtocolError;
				Disconnect (ex);
				throw;
			}

			pc.Status = GetCommandStatus (response, out string text);
			pc.StatusText = text;

			switch (pc.Status) {
			case Pop3CommandStatus.ProtocolError:
				var pex = new Pop3ProtocolException (string.Format ("Unexpected response from server: {0}", response));
				Disconnect (pex);
				throw pex;
			case Pop3CommandStatus.Continue:
			case Pop3CommandStatus.Ok:
				if (pc.Handler != null) {
					try {
						await pc.Handler (this, pc, text, true, cancellationToken).ConfigureAwait (false);
					} catch (Exception ex) {
						pc.Status = Pop3CommandStatus.ProtocolError;
						Disconnect (ex);
						throw;
					}
				}
				break;
			}
		}

		void CheckCanRun (CancellationToken cancellationToken)
		{
			CheckConnected ();

			if (cancellationToken.IsCancellationRequested) {
				queue.Clear ();
				cancellationToken.ThrowIfCancellationRequested ();
			}
		}

		/// <summary>
		/// Run the command pipeline.
		/// </summary>
		/// <param name="throwOnError"><c>true</c> if exceptions should be thrown for failed commands; otherwise, <c>false</c>.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="InvalidOperationException">
		/// The engine is not connected.
		/// </exception>
		public void Run (bool throwOnError, CancellationToken cancellationToken)
		{
			CheckCanRun (cancellationToken);

			try {
				for (int i = 0; i < queue.Count; i++) {
					var pc = queue[i];

					pc.Status = Pop3CommandStatus.Active;

					stream.QueueCommand (pc.Encoding, pc.Command, cancellationToken);
				}

				stream.Flush (cancellationToken);

				for (int i = 0; i < queue.Count; i++)
					ReadResponse (queue[i], cancellationToken);

				for (int i = 0; i < queue.Count && throwOnError; i++)
					queue[i].ThrowIfError ();
			} finally {
				queue.Clear ();
			}
		}

		/// <summary>
		/// Asynchronously run the command pipeline.
		/// </summary>
		/// <param name="throwOnError"><c>true</c> if exceptions should be thrown for failed commands; otherwise, <c>false</c>.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="InvalidOperationException">
		/// The engine is not connected.
		/// </exception>
		public async Task RunAsync (bool throwOnError, CancellationToken cancellationToken)
		{
			CheckCanRun (cancellationToken);

			try {
				for (int i = 0; i < queue.Count; i++) {
					var pc = queue[i];

					pc.Status = Pop3CommandStatus.Active;

					await stream.QueueCommandAsync (pc.Encoding, pc.Command, cancellationToken).ConfigureAwait (false);
				}

				await stream.FlushAsync (cancellationToken).ConfigureAwait (false);

				for (int i = 0; i < queue.Count; i++)
					await ReadResponseAsync (queue[i], cancellationToken).ConfigureAwait (false);

				for (int i = 0; i < queue.Count && throwOnError; i++)
					queue[i].ThrowIfError ();
			} finally {
				queue.Clear ();
			}
		}

		public Pop3Command QueueCommand (Pop3CommandHandler handler, Encoding encoding, string format, params object[] args)
		{
			var pc = new Pop3Command (handler, encoding, format, args);
			queue.Add (pc);
			return pc;
		}

		public Pop3Command QueueCommand (Pop3CommandHandler handler, string format, params object[] args)
		{
			return QueueCommand (handler, Encoding.ASCII, format, args);
		}

		static bool IsCapability (string capability, string text, int length, bool hasValue = false)
		{
			if (hasValue) {
				if (length < capability.Length)
					return false;
			} else {
				if (length != capability.Length)
					return false;
			}

			if (string.Compare (text, 0, capability, 0, capability.Length, StringComparison.OrdinalIgnoreCase) != 0)
				return false;

			if (hasValue) {
				int index = capability.Length;

				return length == capability.Length || text[index] == ' ' || text[index] == '=';
			}

			return true;
		}

		static bool IsToken (string token, string text, int startIndex, int length)
		{
			return length == token.Length && string.Compare (text, startIndex, token, 0, token.Length, StringComparison.OrdinalIgnoreCase) == 0;
		}

		static bool ReadNextToken (string text, ref int index, out int startIndex, out int length)
		{
			while (index < text.Length && char.IsWhiteSpace (text[index]))
				index++;

			startIndex = index;

			while (index < text.Length && !char.IsWhiteSpace (text[index]))
				index++;

			length = index - startIndex;

			return length > 0;
		}

		void AddAuthenticationMechanisms (string text, int startIndex)
		{
			int index = startIndex;

			while (ReadNextToken (text, ref index, out var tokenIndex, out var length)) {
				var mechanism = text.Substring (tokenIndex, length);

				AuthenticationMechanisms.Add (mechanism);
			}
		}

		static bool TryParseInt32 (string text, int startIndex, int length, out int value)
		{
#if NETSTANDARD2_1_OR_GREATER || NET5_0_OR_GREATER
			var token = text.AsSpan (startIndex, length);
#else
			var token = text.Substring (startIndex, length);
#endif

			return int.TryParse (token, NumberStyles.None, CultureInfo.InvariantCulture, out value);
		}

		static void ParseCapaResponse (Pop3Engine engine, string response)
		{
			int index = response.IndexOf (' ');
			int startIndex, length, value;

			if (index == -1)
				index = response.Length;

			if (IsCapability ("EXPIRE", response, index, true)) {
				engine.Capabilities |= Pop3Capabilities.Expire;

				if (ReadNextToken (response, ref index, out startIndex, out length)) {
					if (IsToken ("NEVER", response, startIndex, length)) {
						engine.ExpirePolicy = -1;
					} else if (TryParseInt32 (response, startIndex, length, out value)) {
						engine.ExpirePolicy = value;
					}
				}
			} else if (IsCapability ("IMPLEMENTATION", response, index, true)) {
				engine.Implementation = response.Substring (index + 1);
			} else if (IsCapability ("LANG", response, index)) {
				engine.Capabilities |= Pop3Capabilities.Lang;
			} else if (IsCapability ("LOGIN-DELAY", response, index, true)) {
				if (ReadNextToken (response, ref index, out startIndex, out length)) {
					if (TryParseInt32 (response, startIndex, length, out value)) {
						engine.Capabilities |= Pop3Capabilities.LoginDelay;
						engine.LoginDelay = value;
					}
				}
			} else if (IsCapability ("PIPELINING", response, index)) {
				engine.Capabilities |= Pop3Capabilities.Pipelining;
			} else if (IsCapability ("RESP-CODES", response, index)) {
				engine.Capabilities |= Pop3Capabilities.ResponseCodes;
			} else if (IsCapability ("SASL", response, index, true)) {
				engine.Capabilities |= Pop3Capabilities.Sasl;
				engine.AddAuthenticationMechanisms (response, index);
			} else if (IsCapability ("STLS", response, index)) {
				engine.Capabilities |= Pop3Capabilities.StartTLS;
			} else if (IsCapability ("TOP", response, index)) {
				engine.Capabilities |= Pop3Capabilities.Top;
			} else if (IsCapability ("UIDL", response, index)) {
				engine.Capabilities |= Pop3Capabilities.UIDL;
			} else if (IsCapability ("USER", response, index)) {
				engine.Capabilities |= Pop3Capabilities.User;
			} else if (IsCapability ("UTF8", response, index, true)) {
				engine.Capabilities |= Pop3Capabilities.UTF8;

				while (ReadNextToken (response, ref index, out startIndex, out length)) {
					if (IsToken ("USER", response, startIndex, length)) {
						engine.Capabilities |= Pop3Capabilities.UTF8User;
					}
				}
			}
		}

		static void ReadCapaResponse (Pop3Engine engine, Pop3Command pc, CancellationToken cancellationToken)
		{
			string response;

			do {
				if ((response = engine.ReadLine (cancellationToken)) == ".")
					break;

				ParseCapaResponse (engine, response);
			} while (true);
		}

		static async Task ReadCapaResponseAsync (Pop3Engine engine, Pop3Command pc, CancellationToken cancellationToken)
		{
			string response;

			do {
				if ((response = await engine.ReadLineAsync (cancellationToken).ConfigureAwait (false)) == ".")
					break;

				ParseCapaResponse (engine, response);
			} while (true);
		}

		static Task ProcessCapaResponse (Pop3Engine engine, Pop3Command pc, string text, bool doAsync, CancellationToken cancellationToken)
		{
			if (pc.Status != Pop3CommandStatus.Ok)
				return Task.CompletedTask;

			if (doAsync)
				return ReadCapaResponseAsync (engine, pc, cancellationToken);

			ReadCapaResponse (engine, pc, cancellationToken);
			return Task.CompletedTask;
		}

		Pop3Command QueueCapabilitiesCommand ()
		{
			CheckConnected ();

			// Clear all CAPA response capabilities (except the APOP, USER, and STLS capabilities).
			Capabilities &= Pop3Capabilities.Apop | Pop3Capabilities.User | Pop3Capabilities.StartTLS;
			AuthenticationMechanisms.Clear ();
			Implementation = null;
			ExpirePolicy = 0;
			LoginDelay = 0;

			return QueueCommand (ProcessCapaResponse, "CAPA\r\n");
		}

		public void QueryCapabilities (CancellationToken cancellationToken)
		{
			QueueCapabilitiesCommand ();

			Run (false, cancellationToken);
		}

		public Task QueryCapabilitiesAsync (CancellationToken cancellationToken)
		{
			QueueCapabilitiesCommand ();

			return RunAsync (false, cancellationToken);
		}
	}
}
