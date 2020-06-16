//
// SmtpDataFilterTests.cs
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

using NUnit.Framework;

using MimeKit.IO;
using MailKit.Net.Smtp;

namespace UnitTests.Net.Smtp {
	[TestFixture]
	public class SmtpDataFilterTests
	{
		const string SimpleDataInput = "This is a simple stream of text\r\nthat has no need to be byte-stuffed\r\nat all.\r\n\r\nPlease don't get byte-stuffed!\r\n";
		const string ComplexDataInput = "This is a bit more complicated\r\n... This line starts with a '.' and\r\ntherefore needs to be byte-stuffed\r\n. And so does this line!\r\n";
		const string ComplexDataOutput = "This is a bit more complicated\r\n.... This line starts with a '.' and\r\ntherefore needs to be byte-stuffed\r\n.. And so does this line!\r\n";

		[Test]
		public void TestSmtpDataFilter ()
		{
			var inputs = new string[] { SimpleDataInput, ComplexDataInput };
			var outputs = new string[] { SimpleDataInput, ComplexDataOutput };
			var filter = new SmtpDataFilter ();

			for (int i = 0; i < inputs.Length; i++) {
				using (var memory = new MemoryStream ()) {
					byte[] buffer;
					int n;

					using (var filtered = new FilteredStream (memory)) {
						filtered.Add (filter);

						buffer = Encoding.ASCII.GetBytes (inputs[i]);
						filtered.Write (buffer, 0, buffer.Length);
						filtered.Flush ();
					}

					buffer = memory.GetBuffer ();
					n = (int) memory.Length;

					var text = Encoding.ASCII.GetString (buffer, 0, n);

					Assert.AreEqual (outputs[i], text);

					filter.Reset ();
				}
			}
		}

		[Test]
		public void TestSmtpDataFilterEnsureNewLine ()
		{
			var inputs = new string[] { SimpleDataInput.TrimEnd (), ComplexDataInput.TrimEnd () };
			var outputs = new string[] { SimpleDataInput, ComplexDataOutput };
			var filter = new SmtpDataFilter ();

			for (int i = 0; i < inputs.Length; i++) {
				using (var memory = new MemoryStream ()) {
					byte[] buffer;
					int n;

					using (var filtered = new FilteredStream (memory)) {
						filtered.Add (filter);

						buffer = Encoding.ASCII.GetBytes (inputs[i]);
						filtered.Write (buffer, 0, buffer.Length);
						filtered.Flush ();
					}

					buffer = memory.GetBuffer ();
					n = (int) memory.Length;

					var text = Encoding.ASCII.GetString (buffer, 0, n);

					Assert.AreEqual (outputs[i], text);

					filter.Reset ();
				}
			}
		}

		[Test]
		public void TestSmtpDataFilterBufferBoundaryNewLine ()
		{
			string output = new string ('x', 78) + "\r\n..hello\r\n";
			var filter = new SmtpDataFilter ();

			using (var memory = new MemoryStream ()) {
				byte[] buffer;
				int n;

				using (var filtered = new FilteredStream (memory)) {
					filtered.Add (filter);

					buffer = Encoding.ASCII.GetBytes (new string ('x', 78) + "\r\n");
					filtered.Write (buffer, 0, buffer.Length);

					buffer = Encoding.ASCII.GetBytes (".hello\r\n");
					filtered.Write (buffer, 0, buffer.Length);

					filtered.Flush ();
				}

				buffer = memory.GetBuffer ();
				n = (int) memory.Length;

				var text = Encoding.ASCII.GetString (buffer, 0, n);

				Assert.AreEqual (output, text);
			}
		}

		[Test]
		public void TestSmtpDataFilterBufferBoundaryNewLineEnsureNewLine ()
		{
			string output = new string ('x', 78) + "\r\n..hello\r\n";
			var filter = new SmtpDataFilter ();

			using (var memory = new MemoryStream ()) {
				byte[] buffer;
				int n;

				using (var filtered = new FilteredStream (memory)) {
					filtered.Add (filter);

					buffer = Encoding.ASCII.GetBytes (new string ('x', 78) + "\r\n");
					filtered.Write (buffer, 0, buffer.Length);

					buffer = Encoding.ASCII.GetBytes (".hello");
					filtered.Write (buffer, 0, buffer.Length);

					filtered.Flush ();
				}

				buffer = memory.GetBuffer ();
				n = (int) memory.Length;

				var text = Encoding.ASCII.GetString (buffer, 0, n);

				Assert.AreEqual (output, text);
			}
		}

		[Test]
		public void TestSmtpDataFilterBufferBoundaryNonNewLine ()
		{
			string output = new string ('x', 72) + ".hello\r\n";
			var filter = new SmtpDataFilter ();

			using (var memory = new MemoryStream ()) {
				byte[] buffer;
				int n;

				using (var filtered = new FilteredStream (memory)) {
					filtered.Add (filter);

					buffer = Encoding.ASCII.GetBytes (new string ('x', 72));
					filtered.Write (buffer, 0, buffer.Length);

					buffer = Encoding.ASCII.GetBytes (".hello\r\n");
					filtered.Write (buffer, 0, buffer.Length);

					filtered.Flush ();
				}

				buffer = memory.GetBuffer ();
				n = (int) memory.Length;

				var text = Encoding.ASCII.GetString (buffer, 0, n);

				Assert.AreEqual (output, text);
			}
		}

		[Test]
		public void TestSmtpDataFilterBufferBoundaryNonNewLineEnsureNewLine ()
		{
			string output = new string ('x', 72) + ".hello\r\n";
			var filter = new SmtpDataFilter ();

			using (var memory = new MemoryStream ()) {
				byte[] buffer;
				int n;

				using (var filtered = new FilteredStream (memory)) {
					filtered.Add (filter);

					buffer = Encoding.ASCII.GetBytes (new string ('x', 72));
					filtered.Write (buffer, 0, buffer.Length);

					buffer = Encoding.ASCII.GetBytes (".hello");
					filtered.Write (buffer, 0, buffer.Length);

					filtered.Flush ();
				}

				buffer = memory.GetBuffer ();
				n = (int) memory.Length;

				var text = Encoding.ASCII.GetString (buffer, 0, n);

				Assert.AreEqual (output, text);
			}
		}
	}
}
