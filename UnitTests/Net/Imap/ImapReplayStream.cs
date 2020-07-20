//
// ImapReplayStream.cs
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
using System.Threading.Tasks;
using System.Collections.Generic;

using NUnit.Framework;

using MimeKit.IO;
using MimeKit.IO.Filters;

using MailKit;

namespace UnitTests.Net.Imap {
	enum ImapReplayCommandResponse {
		OK,
		NO,
		BAD,
		Plus
	}

	class ImapReplayFilter : MimeFilterBase
	{
		readonly byte[] variable;
		readonly byte[] value;

		public ImapReplayFilter (string variable, string value)
		{
			this.variable = Encoding.ASCII.GetBytes (variable);
			this.value = Encoding.ASCII.GetBytes (value);
		}

		protected override byte[] Filter (byte[] input, int startIndex, int length, out int outputIndex, out int outputLength, bool flush)
		{
			int endIndex = startIndex + length;
			int copyIndex = startIndex;
			int copyLength = 0;

			for (int index = startIndex; index < endIndex - variable.Length; index++) {
				var matched = true;

				for (int i = 0; i < variable.Length; i++) {
					if (input[index + i] != variable[i]) {
						matched = false;
						break;
					}
				}

				if (!matched)
					continue;

				int n = index - copyIndex;

				EnsureOutputSize (copyLength + n + value.Length, true);
				Buffer.BlockCopy (input, copyIndex, OutputBuffer, copyLength, n);
				index += variable.Length;
				copyIndex = index;
				copyLength += n;

				Buffer.BlockCopy (value, 0, OutputBuffer, copyLength, value.Length);
				copyLength += value.Length;
			}

			if (flush) {
				if (copyLength == 0) {
					outputIndex = startIndex;
					outputLength = length;

					return input;
				}

				int n = endIndex - copyIndex;

				EnsureOutputSize (copyLength + n, true);
				Buffer.BlockCopy (input, copyIndex, OutputBuffer, copyLength, n);
				copyLength += n;

				outputLength = copyLength;
				outputIndex = 0;

				return OutputBuffer;
			} else {
				int n = Math.Min (variable.Length, length);

				SaveRemainingInput (input, endIndex - n, n);

				if (copyLength == 0) {
					outputLength = length - n;
					outputIndex = startIndex;

					return input;
				}

				endIndex -= n;

				if (endIndex > copyIndex) {
					n = endIndex - copyIndex;
					EnsureOutputSize (copyLength + n, true);
					Buffer.BlockCopy (input, copyIndex, OutputBuffer, copyLength, n);
					copyLength += n;
				}

				outputLength = copyLength;
				outputIndex = 0;

				return OutputBuffer;
			}
		}

		public override void Reset ()
		{
			base.Reset ();
		}
	}

	class ImapReplayCommand
	{
		static readonly Encoding Latin1 = Encoding.GetEncoding (28591);

		public Encoding Encoding { get; private set; }
		public byte[] CommandBuffer { get; private set; }
		public string Command { get; private set; }
		public byte[] Response { get; private set; }
		public bool Compressed { get; private set; }

		public ImapReplayCommand (string command, byte[] response, bool compressed = false) : this (Latin1, command, response, compressed)
		{
		}

		public ImapReplayCommand (Encoding encoding, string command, byte[] response, bool compressed = false)
		{
			CommandBuffer = encoding.GetBytes (command);
			Compressed = compressed;
			Response = response;
			Encoding = encoding;
			Command = command;

			if (compressed) {
				using (var memory = new MemoryStream ()) {
					using (var compress = new CompressedStream (memory)) {
						compress.Write (CommandBuffer, 0, CommandBuffer.Length);
						compress.Flush ();

						CommandBuffer = memory.ToArray ();
					}
				}
			}
		}

		public ImapReplayCommand (string command, string resource, bool compressed = false) : this (Latin1, command, resource, compressed)
		{
		}

		public ImapReplayCommand (Encoding encoding, string command, string resource, bool compressed = false)
		{
			string tag = null;

			CommandBuffer = encoding.GetBytes (command);
			Compressed = compressed;
			Encoding = encoding;
			Command = command;

			if (command.StartsWith ("A00000", StringComparison.Ordinal))
				tag = command.Substring (0, 9);

			using (var stream = GetType ().Assembly.GetManifestResourceStream ("UnitTests.Net.Imap.Resources." + resource)) {
				using (var memory = new MemoryBlockStream ()) {
					using (Stream compress = new CompressedStream (memory)) {
						using (var filtered = new FilteredStream (compressed ? compress : memory)) {
							if (tag != null)
								filtered.Add (new ImapReplayFilter ("A########", tag));

							filtered.Add (new Unix2DosFilter ());
							stream.CopyTo (filtered, 4096);
							filtered.Flush ();
						}

						Response = memory.ToArray ();
					}
				}
			}

			if (compressed) {
				using (var memory = new MemoryStream ()) {
					using (var compress = new CompressedStream (memory)) {
						compress.Write (CommandBuffer, 0, CommandBuffer.Length);
						compress.Flush ();

						CommandBuffer = memory.ToArray ();
					}
				}
			}
		}

		public ImapReplayCommand (string tag, string command, string resource, bool compressed = false) : this (Latin1, tag, command, resource, compressed)
		{
		}

		public ImapReplayCommand (Encoding encoding, string tag, string command, string resource, bool compressed = false)
		{
			CommandBuffer = encoding.GetBytes (command);
			Compressed = compressed;
			Encoding = encoding;
			Command = command;

			using (var stream = GetType ().Assembly.GetManifestResourceStream ("UnitTests.Net.Imap.Resources." + resource)) {
				using (var memory = new MemoryBlockStream ()) {
					using (Stream compress = new CompressedStream (memory)) {
						using (var filtered = new FilteredStream (compressed ? compress : memory)) {
							filtered.Add (new ImapReplayFilter ("A########", tag));
							filtered.Add (new Unix2DosFilter ());
							stream.CopyTo (filtered, 4096);
							filtered.Flush ();
						}

						Response = memory.ToArray ();
					}
				}
			}

			if (compressed) {
				using (var memory = new MemoryStream ()) {
					using (var compress = new CompressedStream (memory)) {
						compress.Write (CommandBuffer, 0, CommandBuffer.Length);
						compress.Flush ();

						CommandBuffer = memory.ToArray ();
					}
				}
			}
		}

		public ImapReplayCommand (string command, ImapReplayCommandResponse response, bool compressed = false) : this (Latin1, command, response, compressed)
		{
		}

		public ImapReplayCommand (Encoding encoding, string command, ImapReplayCommandResponse response, bool compressed = false)
		{
			CommandBuffer = encoding.GetBytes (command);
			Compressed = compressed;
			Encoding = encoding;
			Command = command;

			string text;

			if (response == ImapReplayCommandResponse.Plus) {
				text = "+\r\n";
			} else {
				var tokens = command.Split (' ');
				var cmd = (tokens [1] == "UID" ? tokens [2] : tokens [1]).TrimEnd ();
				var tag = tokens [0];

				text = string.Format ("{0} {1} {2} {3}\r\n", tag, response, cmd, response == ImapReplayCommandResponse.OK ? "completed" : "failed");
			}

			if (compressed) {
				using (var memory = new MemoryStream ()) {
					using (var compress = new CompressedStream (memory)) {
						var buffer = encoding.GetBytes (text);

						compress.Write (buffer, 0, buffer.Length);
						compress.Flush ();

						Response = memory.ToArray ();
					}
				}

				using (var memory = new MemoryStream ()) {
					using (var compress = new CompressedStream (memory)) {
						compress.Write (CommandBuffer, 0, CommandBuffer.Length);
						compress.Flush ();

						CommandBuffer = memory.ToArray ();
					}
				}
			} else {
				Response = encoding.GetBytes (text);
			}
		}
	}

	enum ImapReplayState {
		SendResponse,
		WaitForCommand,
	}

	class ImapReplayStream : Stream
	{
		readonly MemoryStream sent = new MemoryStream ();
		readonly IList<ImapReplayCommand> commands;
		readonly bool testUnixFormat;
		ImapReplayState state;
		int timeout = 100000;
		Stream stream;
		bool disposed;
		bool asyncIO;
		bool isAsync;
		bool done;
		int index;

		public ImapReplayStream (IList<ImapReplayCommand> commands, bool asyncIO, bool testUnixFormat = false)
		{
			stream = GetResponseStream (commands[0]);
			state = ImapReplayState.SendResponse;
			this.testUnixFormat = testUnixFormat;
			this.commands = commands;
			this.asyncIO = asyncIO;
		}

		void CheckDisposed ()
		{
			if (disposed)
				throw new ObjectDisposedException ("ImapReplayStream");
		}

		#region implemented abstract members of Stream

		public override bool CanRead {
			get { return true; }
		}

		public override bool CanSeek {
			get { return true; }
		}

		public override bool CanWrite {
			get { return true; }
		}

		public override bool CanTimeout {
			get { return true; }
		}

		public override long Length {
			get { return stream.Length; }
		}

		public override long Position {
			get { return stream.Position; }
			set { throw new NotSupportedException (); }
		}

		public override int ReadTimeout {
			get { return timeout; }
			set { timeout = value; }
		}

		public override int WriteTimeout {
			get { return timeout; }
			set { timeout = value; }
		}

		public override int Read (byte[] buffer, int offset, int count)
		{
			CheckDisposed ();

			if (asyncIO) {
				Assert.IsTrue (isAsync, "Trying to Read in an async unit test.");
			} else {
				Assert.IsFalse (isAsync, "Trying to ReadAsync in a non-async unit test.");
			}

			if (state != ImapReplayState.SendResponse) {
				if (index >= commands.Count)
					return 0;

				var command = GetSentCommand ();

				Assert.AreEqual (ImapReplayState.SendResponse, state, "Trying to read before command received. Sent so far: {0}", command);
			}
			Assert.IsNotNull (stream, "Trying to read when no data available.");

			int nread = stream.Read (buffer, offset, count);

			if (stream.Position == stream.Length) {
				state = ImapReplayState.WaitForCommand;
				index++;
			}

			return nread;
		}

		public override Task<int> ReadAsync (byte[] buffer, int offset, int count, CancellationToken cancellationToken)
		{
			isAsync = true;

			try {
				return Task.FromResult (Read (buffer, offset, count));
			} finally {
				isAsync = false;
			}
		}

		Stream GetResponseStream (ImapReplayCommand command)
		{
			MemoryStream memory;

			if (testUnixFormat && !command.Compressed) {
				memory = new MemoryStream ();

				using (var filtered = new FilteredStream (memory)) {
					filtered.Add (new Dos2UnixFilter ());
					filtered.Write (command.Response, 0, command.Response.Length);
					filtered.Flush ();
				}

				memory.Position = 0;
			} else {
				memory = new MemoryStream (command.Response, false);
			}

			return memory;
		}

		string GetSentCommand ()
		{
			if (!commands[index].Compressed)
				return commands[index].Encoding.GetString (sent.GetBuffer (), 0, (int) sent.Length);

			using (var memory = new MemoryStream (sent.GetBuffer (), 0, (int) sent.Length)) {
				using (var compressed = new CompressedStream (memory)) {
					using (var decompressed = new MemoryStream ()) {
						compressed.CopyTo (decompressed, 4096);

						return commands[index].Encoding.GetString (decompressed.GetBuffer (), 0, (int) decompressed.Length);
					}
				}
			}
		}

		public override void Write (byte[] buffer, int offset, int count)
		{
			CheckDisposed ();

			if (asyncIO) {
				if (count != 6 || Encoding.ASCII.GetString (buffer, offset, count) != "DONE\r\n")
					Assert.IsTrue (isAsync, "Trying to Write in an async unit test.");
				else
					done = true;
			} else {
				if (count != 6 || Encoding.ASCII.GetString (buffer, offset, count) != "DONE\r\n")
					Assert.IsFalse (isAsync, "Trying to WriteAsync in a non-async unit test.");
				else
					done = true;
			}

			Assert.AreEqual (ImapReplayState.WaitForCommand, state, "Trying to write when a command has already been given.");

			sent.Write (buffer, offset, count);

			if (sent.Length >= commands[index].CommandBuffer.Length) {
				var command = GetSentCommand ();

				Assert.AreEqual (commands[index].Command, command, "Commands did not match.");

				if (stream != null)
					stream.Dispose ();

				stream = GetResponseStream (commands[index]);
				state = ImapReplayState.SendResponse;
				sent.SetLength (0);
			}
		}

		public override Task WriteAsync (byte[] buffer, int offset, int count, CancellationToken cancellationToken)
		{
			isAsync = true;

			try {
				Write (buffer, offset, count);
				return Task.FromResult (true);
			} finally {
				isAsync = false;
			}
		}

		public override void Flush ()
		{
			CheckDisposed ();

			Assert.IsFalse (asyncIO && !done, "Trying to Flush in an async unit test.");
			done = false;
		}

		public override Task FlushAsync (CancellationToken cancellationToken)
		{
			CheckDisposed ();

			Assert.IsTrue (asyncIO || done, "Trying to FlushAsync in a non-async unit test.");
			done = false;

			return Task.FromResult (true);
		}

		public override long Seek (long offset, SeekOrigin origin)
		{
			throw new NotSupportedException ();
		}

		public override void SetLength (long value)
		{
			throw new NotSupportedException ();
		}

		#endregion

		protected override void Dispose (bool disposing)
		{
			if (stream != null)
				stream.Dispose ();

			base.Dispose (disposing);
			disposed = true;
		}
	}
}
