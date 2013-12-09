//
// Pop3Engine.cs
//
// Author: Jeffrey Stedfast <jeff@xamarin.com>
//
// Copyright (c) 2013 Jeffrey Stedfast
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
	enum Pop3EngineState {
		Connect,
		Authenticate,
		Transaction,
		Update
	}

	class Pop3Engine
	{
		static readonly Encoding Latin1 = Encoding.GetEncoding (28591);
		readonly List<Pop3Command> queue;
		Pop3Stream stream;
		int nextId;

		public Pop3Engine ()
		{
			AuthenticationMechanisms = new HashSet<string> ();
			Capabilities = Pop3Capabilities.User;
			queue = new List<Pop3Command> ();
			nextId = 1;
		}

		public HashSet<string> AuthenticationMechanisms {
			get; private set;
		}

		public Pop3Capabilities Capabilities {
			get; set;
		}

		public Pop3Stream Stream {
			get { return stream; }
		}

		public Pop3EngineState State {
			get; set;
		}

		public bool IsConnected {
			get { return stream != null && stream.IsConnected; }
		}

		public string ApopToken {
			get; private set;
		}

		public int LoginDelay {
			get; private set;
		}

		public void Connect (Pop3Stream pop3)
		{
			if (stream != null)
				stream.Dispose ();

			Capabilities = Pop3Capabilities.User;
			AuthenticationMechanisms.Clear ();
			State = Pop3EngineState.Connect;
			ApopToken = null;
			stream = pop3;

			// read the pop3 server greeting
			var greeting = ReadLine ().TrimEnd ();

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
				stream = null;

				throw new Pop3Exception (string.Format ("Unexpected greeting from server: {0}", greeting));
			}

			index = text.IndexOf ('>');
			if (text.Length > 0 && text[0] == '<' && index != -1) {
				ApopToken = text.Substring (1, index - 1);
				Capabilities |= Pop3Capabilities.Apop;
			}

			State = Pop3EngineState.Authenticate;
		}

		public void Disconnect ()
		{
			State = Pop3EngineState.Connect;

			if (stream != null) {
				stream.Dispose ();
				stream = null;
			}
		}

		public string ReadLine ()
		{
			if (stream == null)
				throw new InvalidOperationException ();

			using (var memory = new MemoryStream ()) {
				int offset, count;
				byte[] buf;

				while (!stream.ReadLine (out buf, out offset, out count))
					memory.Write (buf, offset, count);

				memory.Write (buf, offset, count);

				count = (int) memory.Length;
				buf = memory.GetBuffer ();

				var line = Latin1.GetString (buf, 0, count);

				#if DEBUG
				Console.Write ("S: {0}", line);
				#endif

				return line;
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

			#if DEBUG
			Console.WriteLine ("C: {0}", pc.Command);
			#endif

			buf = Encoding.UTF8.GetBytes (pc.Command + "\r\n");
			stream.Write (buf, 0, buf.Length);

			try {
				response = ReadLine ().TrimEnd ();
			} catch (IOException) {
				pc.Status = Pop3CommandStatus.ProtocolError;
				throw;
			}

			pc.Status = GetCommandStatus (response, out text);
			switch (pc.Status) {
			case Pop3CommandStatus.ProtocolError:
				throw new Pop3Exception (string.Format ("Unexpected response from server: {0}", response));
			case Pop3CommandStatus.Continue:
			case Pop3CommandStatus.Ok:
				if (pc.Handler != null)
					pc.Handler (this, pc, text);
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
				pc.CancelToken.ThrowIfCancellationRequested ();
			} catch (OperationCanceledException) {
				queue.RemoveAll (x => x.CancelToken == pc.CancelToken);
				throw;
			}

			pc.Status = Pop3CommandStatus.Active;
			ProcessCommand (pc);

			return pc.Id;
		}

		public Pop3Command QueueCommand (CancellationToken token, string format, params object[] args)
		{
			var pc = new Pop3Command (token, format, args);
			pc.Id = nextId++;
			queue.Add (pc);
			return pc;
		}

		static void CapaHandler (Pop3Engine engine, Pop3Command pc, string text)
		{
			if (pc.Status != Pop3CommandStatus.Ok)
				return;

			// clear all CAPA response capabilities
			engine.Capabilities &= ~Pop3Capabilities.CapaMask;
			engine.AuthenticationMechanisms.Clear ();

			string response;

			do {
				if ((response = engine.ReadLine ().TrimEnd ()) == ".")
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

		public Pop3CommandStatus QueryCapabilities (CancellationToken token)
		{
			if (stream == null)
				throw new InvalidOperationException ();

			var pc = QueueCommand (token, "CAPA");
			pc.Handler = CapaHandler;

			while (Iterate () < pc.Id) {
				// continue processing commands...
			}

			return pc.Status;
		}
	}
}
