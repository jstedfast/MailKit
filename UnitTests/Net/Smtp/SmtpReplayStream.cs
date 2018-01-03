//
// SmtpReplayStream.cs
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

using NUnit.Framework;

namespace UnitTests.Net.Smtp {
	class SmtpReplayCommand
	{
		public string Command { get; private set; }
		public string Resource { get; private set; }

		public SmtpReplayCommand (string command, string resource)
		{
			Command = command;
			Resource = resource;
		}
	}

	enum SmtpReplayState {
		SendResponse,
		WaitForCommand,
		WaitForEndOfData,
	}

	class SmtpReplayStream : Stream
	{
		readonly MemoryStream sent = new MemoryStream ();
		readonly IList<SmtpReplayCommand> commands;
		int timeout = 100000;
		SmtpReplayState state;
		Stream stream;
		bool disposed;
		bool asyncIO;
		bool isAsync;
		int index;

		public SmtpReplayStream (IList<SmtpReplayCommand> commands, bool asyncIO)
		{
			stream = GetResourceStream (commands[0].Resource);
			state = SmtpReplayState.SendResponse;
			this.commands = commands;
			this.asyncIO = asyncIO;
		}

		void CheckDisposed ()
		{
			if (disposed)
				throw new ObjectDisposedException ("SmtpReplayStream");
		}

		#region implemented abstract members of Stream

		public override bool CanRead {
			get { return true; }
		}

		public override bool CanSeek {
			get { return false; }
		}

		public override bool CanWrite {
			get { return true; }
		}

		public override bool CanTimeout {
			get { return true; }
		}

		public override long Length {
			get { throw new NotSupportedException (); }
		}

		public override long Position {
			get { throw new NotSupportedException (); }
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

			Assert.AreEqual (SmtpReplayState.SendResponse, state, "Trying to read when no command given.");
			Assert.IsNotNull (stream, "Trying to read when no data available.");

			int nread = stream.Read (buffer, offset, count);

			if (stream.Position == stream.Length) {
				state = commands[index].Command == "DATA\r\n" ? SmtpReplayState.WaitForEndOfData : SmtpReplayState.WaitForCommand;
				stream.Dispose ();
				stream = null;
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

		Stream GetResourceStream (string name)
		{
			return GetType ().Assembly.GetManifestResourceStream ("UnitTests.Net.Smtp.Resources." + name);
		}

		public override void Write (byte[] buffer, int offset, int count)
		{
			CheckDisposed ();

			if (asyncIO) {
				Assert.IsTrue (isAsync, "Trying to Write in an async unit test.");
			} else {
				Assert.IsFalse (isAsync, "Trying to WriteAsync in a non-async unit test.");
			}

			Assert.AreNotEqual (SmtpReplayState.SendResponse, state, "Trying to write when a command has already been given.");

			sent.Write (buffer, offset, count);

			if (sent.Length >= commands[index].Command.Length) {
				var command = Encoding.UTF8.GetString (sent.GetBuffer (), 0, (int) sent.Length);

				if (state == SmtpReplayState.WaitForCommand) {
					Assert.AreEqual (commands[index].Command, command, "Commands did not match.");

					stream = GetResourceStream (commands[index].Resource);
					state = SmtpReplayState.SendResponse;
				} else if (command == "\r\n.\r\n") {
					stream = GetResourceStream (commands[index].Resource);
					state = SmtpReplayState.SendResponse;
				}

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

			Assert.IsFalse (asyncIO, "Trying to Flush in an async unit test.");
		}

		public override Task FlushAsync (CancellationToken cancellationToken)
		{
			CheckDisposed ();

			Assert.IsTrue (asyncIO, "Trying to FlushAsync in a non-async unit test.");

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
			base.Dispose (disposing);
			disposed = true;
		}
	}
}
	