//
// Pop3Engine.cs
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
using System.Collections.Generic;

namespace MailKit.Net.Pop3 {
	/// <summary>
	/// The state of the <see cref="Pop3Engine"/>.
	/// </summary>
	enum Pop3EngineState {
		/// <summary>
		/// The connection state indicates that engine is waiting to connect.
		/// </summary>
		Connection,

		/// <summary>
		/// The authentication state indicates that the engine has not yet authenticated.
		/// </summary>
		Authentication,

		/// <summary>
		/// The transaction state indicates that the engine is authenticated and may
		/// retrieve messages from the server.
		/// </summary>
		Transaction
	}

	/// <summary>
	/// A POP3 command engine.
	/// </summary>
	class Pop3Engine
	{
		static readonly Encoding Latin1 = Encoding.GetEncoding (28591);
		readonly List<Pop3Command> queue;
		Pop3Stream stream;
		int nextId;

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
		/// Gets the authentication mechanisms supported by the POP3 server.
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
		/// Gets the capabilities supported by the POP3 server.
		/// </summary>
		/// <remarks>
		/// The capabilities will not be known until a successful connection
		/// has been made via the <see cref="Connect"/> method.
		/// </remarks>
		/// <value>The capabilities.</value>
		public Pop3Capabilities Capabilities {
			get; set;
		}

		/// <summary>
		/// Gets the underlying POP3 stream.
		/// </summary>
		/// <value>The pop3 stream.</value>
		public Pop3Stream Stream {
			get { return stream; }
		}

		/// <summary>
		/// Gets or sets the state of the engine.
		/// </summary>
		/// <value>The engine state.</value>
		public Pop3EngineState State {
			get; set;
		}

		/// <summary>
		/// Gets whether or not the engine is currently connected to a POP3 server.
		/// </summary>
		/// <value><c>true</c> if the engine is connected; otherwise, <c>false</c>.</value>
		public bool IsConnected {
			get { return stream != null && stream.IsConnected; }
		}

		/// <summary>
		/// Gets the APOP authentication token.
		/// </summary>
		/// <value>The APOP authentication token.</value>
		public string ApopToken {
			get; private set;
		}

		/// <summary>
		/// Gets the EXPIRE extension policy value.
		/// </summary>
		/// <value>The EXPIRE policy.</value>
		public int ExpirePolicy {
			get; private set;
		}

		/// <summary>
		/// Gets the implementation details of the server.
		/// </summary>
		/// <value>The implementation details.</value>
		public string Implementation {
			get; private set;
		}

		/// <summary>
		/// Gets the login delay.
		/// </summary>
		/// <value>The login delay.</value>
		public int LoginDelay {
			get; private set;
		}

		/// <summary>
		/// Takes posession of the <see cref="Pop3Stream"/> and reads the greeting.
		/// </summary>
		/// <param name="pop3">The pop3 stream.</param>
		/// <param name="cancellationToken">A cancellation token</param>
		public void Connect (Pop3Stream pop3, CancellationToken cancellationToken)
		{
			if (stream != null)
				stream.Dispose ();

			Capabilities = Pop3Capabilities.User;
			AuthenticationMechanisms.Clear ();
			State = Pop3EngineState.Connection;
			ApopToken = null;
			stream = pop3;

			// read the pop3 server greeting
			var greeting = ReadLine (cancellationToken).TrimEnd ();

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

			index = text.IndexOf ('>');
			if (text.Length > 0 && text[0] == '<' && index != -1) {
				ApopToken = text.Substring (1, index - 1);
				Capabilities |= Pop3Capabilities.Apop;
			}

			State = Pop3EngineState.Authentication;
		}

		/// <summary>
		/// Disconnects the <see cref="Pop3Stream"/>.
		/// </summary>
		public void Disconnect ()
		{
			State = Pop3EngineState.Connection;

			if (stream != null) {
				stream.Dispose ();
				stream = null;
			}
		}

		/// <summary>
		/// Reads a single line from the <see cref="Pop3Stream"/>.
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

				return Latin1.GetString (buf, 0, count);
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

		void ProcessCommand (Pop3Command pc)
		{
			string response, text;
			byte[] buf;

			buf = Encoding.UTF8.GetBytes (pc.Command + "\r\n");
			stream.Write (buf, 0, buf.Length);

			try {
				response = ReadLine (pc.CancellationToken).TrimEnd ();
			} catch {
				pc.Status = Pop3CommandStatus.ProtocolError;
				Disconnect ();
				throw;
			}

			pc.Status = GetCommandStatus (response, out text);
			switch (pc.Status) {
			case Pop3CommandStatus.ProtocolError:
				Disconnect ();
				throw new Pop3ProtocolException (string.Format ("Unexpected response from server: {0}", response));
			case Pop3CommandStatus.Continue:
			case Pop3CommandStatus.Ok:
				if (pc.Handler != null) {
					try {
						pc.Handler (this, pc, text);
					} catch {
						pc.Status = Pop3CommandStatus.ProtocolError;
						Disconnect ();
						throw;
					}
				}
				break;
			}
		}

		public int Iterate ()
		{
			if (stream == null)
				throw new InvalidOperationException ();

			if (queue.Count == 0)
				return 0;

			var pc = queue[0];
			queue.RemoveAt (0);

			try {
				pc.CancellationToken.ThrowIfCancellationRequested ();
			} catch (OperationCanceledException) {
				queue.RemoveAll (x => x.CancellationToken == pc.CancellationToken);
				throw;
			}

			pc.Status = Pop3CommandStatus.Active;
			ProcessCommand (pc);

			return pc.Id;
		}

		public Pop3Command QueueCommand (CancellationToken cancellationToken, string format, params object[] args)
		{
			var pc = new Pop3Command (cancellationToken, format, args);
			pc.Id = nextId++;
			queue.Add (pc);
			return pc;
		}

		static void CapaHandler (Pop3Engine engine, Pop3Command pc, string text)
		{
			// clear all CAPA response capabilities (except the APOP capability)
			engine.Capabilities &= Pop3Capabilities.Apop;
			engine.AuthenticationMechanisms.Clear ();
			engine.Implementation = null;
			engine.ExpirePolicy = 0;
			engine.LoginDelay = 0;

			if (pc.Status != Pop3CommandStatus.Ok)
				return;

			string response;

			do {
				if ((response = engine.ReadLine (pc.CancellationToken).TrimEnd ()) == ".")
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
				}
			} while (true);
		}

		public Pop3CommandStatus QueryCapabilities (CancellationToken cancellationToken)
		{
			if (stream == null)
				throw new InvalidOperationException ();

			var pc = QueueCommand (cancellationToken, "CAPA");
			pc.Handler = CapaHandler;

			while (Iterate () < pc.Id) {
				// continue processing commands...
			}

			return pc.Status;
		}
	}
}
