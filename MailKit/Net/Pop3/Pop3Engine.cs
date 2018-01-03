//
// Pop3Engine.cs
//
// Author: Jeffrey Stedfast <jestedfa@microsoft.com>
//
// Copyright (c) 2013-2018 Xamarin Inc. (www.xamarin.com)
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
using System.Threading.Tasks;
using System.Collections.Generic;

#if NETFX_CORE
using Encoding = Portable.Text.Encoding;
using EncoderExceptionFallback = Portable.Text.EncoderExceptionFallback;
using DecoderExceptionFallback = Portable.Text.DecoderExceptionFallback;
using DecoderFallbackException = Portable.Text.DecoderFallbackException;
#endif

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
		static readonly Encoding Latin1;
		static readonly Encoding UTF8;

		readonly List<Pop3Command> queue;
		Pop3Stream stream;
		int nextId;

		static Pop3Engine ()
		{
			UTF8 = Encoding.GetEncoding (65001, new EncoderExceptionFallback (), new DecoderExceptionFallback ());

			try {
				Latin1 = Encoding.GetEncoding (28591);
			} catch (NotSupportedException) {
				Latin1 = Encoding.GetEncoding (1252);
			}
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="MailKit.Net.Pop3.Pop3Engine"/> class.
		/// </summary>
		public Pop3Engine ()
		{
			AuthenticationMechanisms = new HashSet<string> ();
			Capabilities = Pop3Capabilities.User;
			queue = new List<Pop3Command> ();
			nextId = 1;
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

		async Task ConnectAsync (Pop3Stream pop3, bool doAsync, CancellationToken cancellationToken)
		{
			if (stream != null)
				stream.Dispose ();

			Capabilities = Pop3Capabilities.User;
			AuthenticationMechanisms.Clear ();
			State = Pop3EngineState.Disconnected;
			ApopToken = null;
			stream = pop3;

			// read the pop3 server greeting
			var greeting = (await ReadLineAsync (doAsync, cancellationToken).ConfigureAwait (false)).TrimEnd ();

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
			ConnectAsync (pop3, false, cancellationToken).GetAwaiter ().GetResult ();
		}

		/// <summary>
		/// Takes posession of the <see cref="Pop3Stream"/> and reads the greeting.
		/// </summary>
		/// <remarks>
		/// Takes posession of the <see cref="Pop3Stream"/> and reads the greeting.
		/// </remarks>
		/// <param name="pop3">The pop3 stream.</param>
		/// <param name="cancellationToken">The cancellation token</param>
		public Task ConnectAsync (Pop3Stream pop3, CancellationToken cancellationToken)
		{
			return ConnectAsync (pop3, true, cancellationToken);
		}

		public event EventHandler<EventArgs> Disconnected;

		void OnDisconnected ()
		{
			var handler = Disconnected;

			if (handler != null)
				handler (this, EventArgs.Empty);
		}

		/// <summary>
		/// Disconnects the <see cref="Pop3Engine"/>.
		/// </summary>
		/// <remarks>
		/// Disconnects the <see cref="Pop3Engine"/>.
		/// </remarks>
		public void Disconnect ()
		{
			Uri = null;

			if (stream != null) {
				stream.Dispose ();
				stream = null;
			}

			if (State != Pop3EngineState.Disconnected) {
				State = Pop3EngineState.Disconnected;
				OnDisconnected ();
			}
		}

		async Task<string> ReadLineAsync (bool doAsync, CancellationToken cancellationToken)
		{
			if (stream == null)
				throw new InvalidOperationException ();

			using (var memory = new MemoryStream ()) {
				bool complete;
				byte[] buf;
				int count;

				do {
					if (doAsync)
						complete = await stream.ReadLineAsync (memory, cancellationToken).ConfigureAwait (false);
					else
						complete = stream.ReadLine (memory, cancellationToken);
				} while (!complete);

				count = (int) memory.Length;
#if !NETFX_CORE && !NETSTANDARD
				buf = memory.GetBuffer ();
#else
				buf = memory.ToArray ();
#endif

				// Trim the <CR><LF> sequence from the end of the line.
				if (buf[count - 1] == (byte) '\n') {
					count--;

					if (buf[count - 1] == (byte) '\r')
						count--;
				}

				try {
					return UTF8.GetString (buf, 0, count);
				} catch (DecoderFallbackException) {
					return Latin1.GetString (buf, 0, count);
				}
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
		/// An I/O error occurred.
		/// </exception>
		public string ReadLine (CancellationToken cancellationToken)
		{
			return ReadLineAsync (false, cancellationToken).GetAwaiter ().GetResult ();
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
		/// An I/O error occurred.
		/// </exception>
		public Task<string> ReadLineAsync (CancellationToken cancellationToken)
		{
			return ReadLineAsync (true, cancellationToken);
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

		async Task SendCommandAsync (Pop3Command pc, bool doAsync, CancellationToken cancellationToken)
		{
			var buf = pc.Encoding.GetBytes (pc.Command + "\r\n");

			if (doAsync)
				await stream.WriteAsync (buf, 0, buf.Length, cancellationToken).ConfigureAwait (false);
			else
				stream.Write (buf, 0, buf.Length, cancellationToken);
		}

		async Task ReadResponseAsync (Pop3Command pc, bool doAsync)
		{
			string response, text;

			try {
				response = (await ReadLineAsync (doAsync, pc.CancellationToken).ConfigureAwait (false)).TrimEnd ();
			} catch {
				pc.Status = Pop3CommandStatus.ProtocolError;
				Disconnect ();
				throw;
			}

			pc.Status = GetCommandStatus (response, out text);
			pc.StatusText = text;

			switch (pc.Status) {
			case Pop3CommandStatus.ProtocolError:
				Disconnect ();
				throw new Pop3ProtocolException (string.Format ("Unexpected response from server: {0}", response));
			case Pop3CommandStatus.Continue:
			case Pop3CommandStatus.Ok:
				if (pc.Handler != null) {
					try {
						await pc.Handler (this, pc, text, doAsync).ConfigureAwait (false);
					} catch {
						pc.Status = Pop3CommandStatus.ProtocolError;
						Disconnect ();
						throw;
					}
				}
				break;
			}
		}

		async Task<int> IterateAsync (bool doAsync)
		{
			if (stream == null)
				throw new InvalidOperationException ();

			if (queue.Count == 0)
				return 0;

			int count = (Capabilities & Pop3Capabilities.Pipelining) != 0 ? queue.Count : 1;
			var cancellationToken = queue[0].CancellationToken;
			var active = new List<Pop3Command> ();

			if (cancellationToken.IsCancellationRequested) {
				queue.RemoveAll (x => x.CancellationToken.IsCancellationRequested);
				cancellationToken.ThrowIfCancellationRequested ();
			}

			for (int i = 0; i < count; i++) {
				var pc = queue[0];

				if (i > 0 && !pc.CancellationToken.Equals (cancellationToken))
					break;

				queue.RemoveAt (0);

				pc.Status = Pop3CommandStatus.Active;
				active.Add (pc);

				await SendCommandAsync (pc, doAsync, cancellationToken).ConfigureAwait (false);
			}

			if (doAsync)
				await stream.FlushAsync (cancellationToken).ConfigureAwait (false);
			else
				stream.Flush (cancellationToken);

			for (int i = 0; i < active.Count; i++)
				await ReadResponseAsync (active[i], doAsync).ConfigureAwait (false);

			return active[active.Count - 1].Id;
		}

		/// <summary>
		/// Iterate the command pipeline.
		/// </summary>
		/// <returns>The ID of the command that just completed.</returns>
		public int Iterate ()
		{
			return IterateAsync (false).GetAwaiter ().GetResult ();
		}

		/// <summary>
		/// Iterate the command pipeline.
		/// </summary>
		/// <returns>The ID of the command that just completed.</returns>
		public Task<int> IterateAsync ()
		{
			return IterateAsync (true);
		}

		public Pop3Command QueueCommand (CancellationToken cancellationToken, Pop3CommandHandler handler, Encoding encoding, string format, params object[] args)
		{
			var pc = new Pop3Command (cancellationToken, handler, encoding, format, args);
			pc.Id = nextId++;
			queue.Add (pc);
			return pc;
		}

		public Pop3Command QueueCommand (CancellationToken cancellationToken, Pop3CommandHandler handler, string format, params object[] args)
		{
			return QueueCommand (cancellationToken, handler, Encoding.ASCII, format, args);
		}

		static async Task CapaHandler (Pop3Engine engine, Pop3Command pc, string text, bool doAsync)
		{
			if (pc.Status != Pop3CommandStatus.Ok)
				return;

			string response;

			do {
				if ((response = await engine.ReadLineAsync (doAsync, pc.CancellationToken).ConfigureAwait (false)) == ".")
					break;

				int index = response.IndexOf (' ');
				string token, data;
				int value;

				if (index != -1) {
					token = response.Substring (0, index);

					while (index < response.Length && char.IsWhiteSpace (response[index]))
						index++;

					if (index < response.Length)
						data = response.Substring (index);
					else
						data = string.Empty;
				} else {
					data = string.Empty;
					token = response;
				}

				switch (token) {
				case "EXPIRE":
					engine.Capabilities |= Pop3Capabilities.Expire;
					var tokens = data.Split (' ');

					if (int.TryParse (tokens[0], out value))
						engine.ExpirePolicy = value;
					else if (tokens[0] == "NEVER")
						engine.ExpirePolicy = -1;
					break;
				case "IMPLEMENTATION":
					engine.Implementation = data;
					break;
				case "LOGIN-DELAY":
					if (int.TryParse (data, out value)) {
						engine.Capabilities |= Pop3Capabilities.LoginDelay;
						engine.LoginDelay = value;
					}
					break;
				case "PIPELINING":
					engine.Capabilities |= Pop3Capabilities.Pipelining;
					break;
				case "RESP-CODES":
					engine.Capabilities |= Pop3Capabilities.ResponseCodes;
					break;
				case "SASL":
					engine.Capabilities |= Pop3Capabilities.Sasl;
					foreach (var authmech in data.Split (new [] { ' ' }, StringSplitOptions.RemoveEmptyEntries))
						engine.AuthenticationMechanisms.Add (authmech);
					break;
				case "STLS":
					engine.Capabilities |= Pop3Capabilities.StartTLS;
					break;
				case "TOP":
					engine.Capabilities |= Pop3Capabilities.Top;
					break;
				case "UIDL":
					engine.Capabilities |= Pop3Capabilities.UIDL;
					break;
				case "USER":
					engine.Capabilities |= Pop3Capabilities.User;
					break;
				case "UTF8":
					engine.Capabilities |= Pop3Capabilities.UTF8;

					foreach (var item in data.Split (' ')) {
						if (item == "USER")
							engine.Capabilities |= Pop3Capabilities.UTF8User;
					}
					break;
				case "LANG":
					engine.Capabilities |= Pop3Capabilities.Lang;
					break;
				}
			} while (true);
		}

		async Task<Pop3CommandStatus> QueryCapabilitiesAsync (bool doAsync, CancellationToken cancellationToken)
		{
			if (stream == null)
				throw new InvalidOperationException ();

			// clear all CAPA response capabilities (except the APOP capability)
			Capabilities &= Pop3Capabilities.Apop;
			AuthenticationMechanisms.Clear ();
			Implementation = null;
			ExpirePolicy = 0;
			LoginDelay = 0;

			var pc = QueueCommand (cancellationToken, CapaHandler, "CAPA");

			while (await IterateAsync (doAsync).ConfigureAwait (false) < pc.Id) {
				// continue processing commands...
			}

			return pc.Status;
		}

		public Pop3CommandStatus QueryCapabilities (CancellationToken cancellationToken)
		{
			return QueryCapabilitiesAsync (false, cancellationToken).GetAwaiter ().GetResult ();
		}

		public Task<Pop3CommandStatus> QueryCapabilitiesAsync (CancellationToken cancellationToken)
		{
			return QueryCapabilitiesAsync (true, cancellationToken);
		}
	}
}
