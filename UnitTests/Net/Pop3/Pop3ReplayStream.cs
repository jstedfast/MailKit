//
// Pop3ReplayStream.cs
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

namespace UnitTests.Net.Pop3 {
	class Pop3ReplayCommand
	{
		public string Command { get; private set; }
		public string Resource { get; private set; }

		public Pop3ReplayCommand (string command, string resource)
		{
			Command = command;
			Resource = resource;
		}
	}

	enum Pop3ReplayState {
		SendResponse,
		WaitForCommand,
	}

	class Pop3ReplayStream : Stream
	{
		readonly IList<Pop3ReplayCommand> commands;
		readonly bool testUnixFormat;
		Pop3ReplayState state;
		int timeout = 100000;
		Stream stream;
		bool disposed;
		int index;

		public Pop3ReplayStream (IList<Pop3ReplayCommand> commands, bool testUnixFormat)
		{
			stream = GetResourceStream (commands[0].Resource);
			state = Pop3ReplayState.SendResponse;
			this.testUnixFormat = testUnixFormat;
			this.commands = commands;
		}

		void CheckDisposed ()
		{
			if (disposed)
				throw new ObjectDisposedException ("Pop3ReplayStream");
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

			Assert.AreEqual (Pop3ReplayState.SendResponse, state, "Trying to read when no command given.");
			Assert.IsNotNull (stream, "Trying to read when no data available.");

			int nread = stream.Read (buffer, offset, 1);

			if (stream.Position == stream.Length) {
				state = Pop3ReplayState.WaitForCommand;
				index++;
			}

			return nread;
		}

		Stream GetResourceStream (string name)
		{
			using (var response = GetType ().Assembly.GetManifestResourceStream ("UnitTests.Net.Pop3.Resources." + name)) {
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

			Assert.AreEqual (Pop3ReplayState.WaitForCommand, state, "Trying to write when a command has already been given.");

			var command = Encoding.UTF8.GetString (buffer, offset, count);

			Assert.AreEqual (commands[index].Command, command, "Commands did not match.");

			if (stream != null)
				stream.Dispose ();

			stream = GetResourceStream (commands[index].Resource);
			state = Pop3ReplayState.SendResponse;
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
