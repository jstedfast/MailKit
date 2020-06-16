//
// ProtocolLoggerTests.cs
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
using MimeKit.IO.Filters;

using MailKit;

namespace UnitTests {
	[TestFixture]
	public class ProtocolLoggerTests
	{
		[Test]
		public void TestArgumentExceptions ()
		{
			Assert.Throws<ArgumentNullException> (() => new ProtocolLogger ((string) null));
			Assert.Throws<ArgumentNullException> (() => new ProtocolLogger ((Stream) null));
			using (var logger = new ProtocolLogger (new MemoryStream ())) {
				var buffer = new byte[1024];

				Assert.Throws<ArgumentNullException> (() => logger.LogConnect (null));
				Assert.Throws<ArgumentNullException> (() => logger.LogClient (null, 0, 0));
				Assert.Throws<ArgumentNullException> (() => logger.LogServer (null, 0, 0));
				Assert.Throws<ArgumentOutOfRangeException> (() => logger.LogClient (buffer, -1, 0));
				Assert.Throws<ArgumentOutOfRangeException> (() => logger.LogServer (buffer, -1, 0));
				Assert.Throws<ArgumentOutOfRangeException> (() => logger.LogClient (buffer, 0, -1));
				Assert.Throws<ArgumentOutOfRangeException> (() => logger.LogServer (buffer, 0, -1));
			}
		}

		[Test]
		public void TestLogging ()
		{
			using (var stream = new MemoryStream ()) {
				using (var logger = new ProtocolLogger (stream, true)) {
					logger.LogConnect (new Uri ("pop://pop.skyfall.net:110"));

					var cmd = Encoding.ASCII.GetBytes ("RETR 1\r\n");
					logger.LogClient (cmd, 0, cmd.Length);

					using (var response = GetType ().Assembly.GetManifestResourceStream ("UnitTests.Net.Pop3.Resources.comcast.retr1.txt")) {
						using (var filtered = new FilteredStream (response)) {
							var buffer = new byte[4096];
							int n;

							filtered.Add (new Unix2DosFilter ());

							while ((n = filtered.Read (buffer, 0, buffer.Length)) > 0)
								logger.LogServer (buffer, 0, n);
						}
					}
				}

				stream.Position = 0;

				using (var reader = new StreamReader (stream)) {
					string line;

					line = reader.ReadLine ();
					Assert.AreEqual ("Connected to pop://pop.skyfall.net:110/", line);

					line = reader.ReadLine ();
					Assert.AreEqual ("C: RETR 1", line);

					using (var response = GetType ().Assembly.GetManifestResourceStream ("UnitTests.Net.Pop3.Resources.comcast.retr1.txt")) {
						using (var r = new StreamReader (response)) {
							string expected;

							while ((expected = r.ReadLine ()) != null) {
								line = reader.ReadLine ();

								Assert.AreEqual ("S: " + expected, line);
							}
						}
					}
				}
			}
		}
	}
}
