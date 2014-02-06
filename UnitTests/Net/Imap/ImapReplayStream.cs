//
// ImapReplayStream.cs
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
using System.Collections.Generic;

using NUnit.Framework;

using MimeKit.IO;
using MimeKit.IO.Filters;

namespace UnitTests.Net.Imap {
	class ImapReplayCommand
	{
		public string Command { get; private set; }
		public string Resource { get; private set; }

		public ImapReplayCommand (string command, string resource)
		{
			Command = command;
			Resource = resource;
		}
	}

	enum ImapReplayState {
		SendResponse,
		WaitForCommand,
	}

	class ImapReplayStream : Stream
	{
		static readonly Encoding Latin1 = Encoding.GetEncoding (28591);
		readonly MemoryStream sent = new MemoryStream ();
		readonly IList<ImapReplayCommand> commands;
		readonly bool testUnixFormat;
		ImapReplayState state;
		Stream stream;
		bool disposed;
		int index;

		public ImapReplayStream (IList<ImapReplayCommand> commands, bool testUnixFormat)
		{
			stream = GetResourceStream (commands[0].Resource);
			state = ImapReplayState.SendResponse;
			this.testUnixFormat = testUnixFormat;
			this.commands = commands;
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

		public override long Length {
			get { return stream.Length; }
		}

		public override long Position {
			get { return stream.Position; }
			set { throw new NotSupportedException (); }
		}

		public override int Read (byte[] buffer, int offset, int count)
		{
			CheckDisposed ();

			Assert.AreEqual (ImapReplayState.SendResponse, state, "Trying to read when no command given.");
			Assert.IsNotNull (stream, "Trying to read when no data available.");

			int nread = stream.Read (buffer, offset, count);

			if (stream.Position == stream.Length) {
				state = ImapReplayState.WaitForCommand;
				index++;
			}

			return nread;
		}

		Stream GetResourceStream (string name)
		{
			using (var response = GetType ().Assembly.GetManifestResourceStream ("UnitTests.Net.Imap.Resources." + name)) {
				var memory = new MemoryBlockStream ();

				using (var filtered = new FilteredStream (memory)) {
					if (testUnixFormat)
						filtered.Add (new Dos2UnixFilter ());
					else
						filtered.Add (new Unix2DosFilter ());
					response.CopyTo (filtered, 4096);
				}

				memory.Position = 0;
				return memory;
			}
		}

		public override void Write (byte[] buffer, int offset, int count)
		{
			CheckDisposed ();

			Assert.AreEqual (ImapReplayState.WaitForCommand, state, "Trying to write when a command has already been given.");

			sent.Write (buffer, offset, count);

			if (sent.Length >= commands[index].Command.Length) {
				var command = Latin1.GetString (sent.GetBuffer (), 0, (int) sent.Length);

				Assert.AreEqual (commands[index].Command, command, "Commands did not match.");

				if (stream != null)
					stream.Dispose ();

				stream = GetResourceStream (commands[index].Resource);
				state = ImapReplayState.SendResponse;
				sent.SetLength (0);
			}
		}

		public override void Flush ()
		{
			CheckDisposed ();
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
