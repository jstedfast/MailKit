//
// ProgressStreamTests.cs
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
using System.Threading;
using System.Threading.Tasks;

using NUnit.Framework;

using MailKit;

using UnitTests.Net;

namespace UnitTests {
	[TestFixture]
	public class ProgressStreamTests
	{
		int progress;

		void Update (int value)
		{
			progress = value;
		}

		[Test]
		public void TestArgumentExceptions ()
		{
			Assert.Throws<ArgumentNullException> (() => new ProgressStream (null, Update));
			Assert.Throws<ArgumentNullException> (() => new ProgressStream (Stream.Null, null));
		}

		[Test]
		public void TestCanReadWriteSeek ()
		{
			using (var stream = new ProgressStream (Stream.Null, Update)) {
				Assert.AreEqual (Stream.Null.CanRead, stream.CanRead);
				Assert.AreEqual (Stream.Null.CanWrite, stream.CanWrite);
				Assert.IsFalse (stream.CanSeek);
				Assert.AreEqual (Stream.Null.CanTimeout, stream.CanTimeout);
			}
		}

		[Test]
		public void TestGetSetTimeouts ()
		{
			using (var stream = new ProgressStream (new DummyNetworkStream (), Update)) {
				stream.ReadTimeout = 5;
				Assert.AreEqual (5, stream.ReadTimeout, "ReadTimeout");

				stream.WriteTimeout = 7;
				Assert.AreEqual (7, stream.WriteTimeout, "WriteTimeout");
			}
		}

		[Test]
		public void TestRead ()
		{
			using (var stream = new ProgressStream (new DummyNetworkStream (), Update)) {
				var buffer = new byte[1024];
				int expected = 517;

				stream.Source.Write (buffer, 0, expected);
				stream.Source.Position = 0;

				progress = 0;
				int n = stream.Read (buffer, 0, buffer.Length);
				Assert.AreEqual (expected, n, "nread");
				Assert.AreEqual (expected, progress, "progress");
			}

			using (var stream = new ProgressStream (new DummyNetworkStream (), Update)) {
				var buffer = new byte[1024];
				int expected = 517;

				stream.Source.Write (buffer, 0, expected);
				stream.Source.Position = 0;

				progress = 0;
				int n = stream.Read (buffer, 0, buffer.Length, CancellationToken.None);
				Assert.AreEqual (expected, n, "nread");
				Assert.AreEqual (expected, progress, "progress");
			}
		}

		[Test]
		public async Task TestReadAsync ()
		{
			using (var stream = new ProgressStream (new DummyNetworkStream (), Update)) {
				var buffer = new byte[1024];
				int expected = 517;

				stream.Source.Write (buffer, 0, expected);
				stream.Source.Position = 0;

				progress = 0;
				int n = await stream.ReadAsync (buffer, 0, buffer.Length);
				Assert.AreEqual (expected, n, "nread");
				Assert.AreEqual (expected, progress, "progress");
			}
		}

		[Test]
		public void TestSeek ()
		{
			using (var stream = new ProgressStream (new DummyNetworkStream (), Update)) {
				Assert.Throws<NotSupportedException> (() => stream.Seek (0, SeekOrigin.Begin));
				Assert.Throws<NotSupportedException> (() => stream.Position = 500);
				Assert.AreEqual (0, stream.Position);
				Assert.AreEqual (0, stream.Length);
			}
		}

		[Test]
		public void TestSetLength ()
		{
			using (var stream = new ProgressStream (new DummyNetworkStream (), Update)) {
				Assert.Throws<NotSupportedException> (() => stream.SetLength (500));
			}
		}

		[Test]
		public void TestWrite ()
		{
			using (var stream = new ProgressStream (new DummyNetworkStream (), Update)) {
				var buffer = new byte[1024];
				int expected = 517;

				progress = 0;
				stream.Write (buffer, 0, expected);
				stream.Flush ();
				Assert.AreEqual (expected, progress, "progress");
			}

			using (var stream = new ProgressStream (new DummyNetworkStream (), Update)) {
				var buffer = new byte[1024];
				int expected = 517;

				progress = 0;
				stream.Write (buffer, 0, expected, CancellationToken.None);
				stream.Flush (CancellationToken.None);
				Assert.AreEqual (expected, progress, "progress");
			}
		}

		[Test]
		public async Task TestWriteAsync ()
		{
			using (var stream = new ProgressStream (new DummyNetworkStream (), Update)) {
				var buffer = new byte[1024];
				int expected = 517;

				progress = 0;
				await stream.WriteAsync (buffer, 0, expected);
				await stream.FlushAsync ();
				Assert.AreEqual (expected, progress, "progress");
			}
		}
	}
}
