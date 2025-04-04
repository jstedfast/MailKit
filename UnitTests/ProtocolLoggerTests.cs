﻿//
// ProtocolLoggerTests.cs
//
// Author: Jeffrey Stedfast <jestedfa@microsoft.com>
//
// Copyright (c) 2013-2025 .NET Foundation and Contributors
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

using System.Text;
using System.Globalization;

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
		public void TestDefaultSettings ()
		{
			Assert.That (ProtocolLogger.DefaultClientPrefix, Is.EqualTo ("C: "), "DefaultClientPrefix");
			Assert.That (ProtocolLogger.DefaultServerPrefix, Is.EqualTo ("S: "), "DefaultServerPrefix");

			using (var logger = new ProtocolLogger (new MemoryStream ())) {
				Assert.That (logger.ClientPrefix, Is.EqualTo ("C: "), "ClientPrefix");
				Assert.That (logger.ServerPrefix, Is.EqualTo ("S: "), "ServerPrefix");
				Assert.That (logger.TimestampFormat, Is.EqualTo ("yyyy-MM-ddTHH:mm:ssZ"), "TimestampFormat");
				Assert.That (logger.LogTimestamps, Is.False, "LogTimestamps");
				Assert.That (logger.RedactSecrets, Is.True, "RedactSecrets");
			}
		}

		[Test]
		public void TestOverridingDefaultSettings ()
		{
			try {
				ProtocolLogger.DefaultClientPrefix = "C> ";
				ProtocolLogger.DefaultServerPrefix = "S> ";

				using (var logger = new ProtocolLogger (new MemoryStream ()) { RedactSecrets = false }) {
					Assert.That (logger.ClientPrefix, Is.EqualTo ("C> "), "ClientPrefix");
					Assert.That (logger.ServerPrefix, Is.EqualTo ("S> "), "ServerPrefix");
					Assert.That (logger.TimestampFormat, Is.EqualTo ("yyyy-MM-ddTHH:mm:ssZ"), "TimestampFormat");
					Assert.That (logger.LogTimestamps, Is.False, "LogTimestamps");
					Assert.That (logger.RedactSecrets, Is.False, "RedactSecrets");
				}
			} finally {
				ProtocolLogger.DefaultClientPrefix = "C: ";
				ProtocolLogger.DefaultServerPrefix = "S: ";
			}
		}

		[Test]
		public void TestLogging ()
		{
			using (var stream = new MemoryStream ()) {
				using (var logger = new ProtocolLogger (stream, true) { RedactSecrets = false }) {
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
					Assert.That (line, Is.EqualTo ("Connected to pop://pop.skyfall.net:110/"));

					line = reader.ReadLine ();
					Assert.That (line, Is.EqualTo ("C: RETR 1"));

					using (var response = GetType ().Assembly.GetManifestResourceStream ("UnitTests.Net.Pop3.Resources.comcast.retr1.txt")) {
						using (var r = new StreamReader (response)) {
							string expected;

							while ((expected = r.ReadLine ()) != null) {
								line = reader.ReadLine ();

								Assert.That (line, Is.EqualTo ("S: " + expected));
							}
						}
					}
				}
			}
		}

		[Test]
		public void TestLogConnectMidline ()
		{
			using (var stream = new MemoryStream ()) {
				using (var logger = new ProtocolLogger (stream, true) { RedactSecrets = false }) {
					byte[] buf;

					buf = Encoding.ASCII.GetBytes ("PARTIAL LINE");
					logger.LogClient (buf, 0, buf.Length);

					logger.LogConnect (new Uri ("proto://server.com"));

					logger.LogServer (buf, 0, buf.Length);

					logger.LogConnect (new Uri ("proto://server.com"));
				}

				var buffer = stream.GetBuffer ();
				int length = (int) stream.Length;

				var result = Encoding.ASCII.GetString (buffer, 0, length);

				Assert.That (result, Is.EqualTo ("C: PARTIAL LINE\r\nConnected to proto://server.com/\r\nS: PARTIAL LINE\r\nConnected to proto://server.com/\r\n"));
			}
		}

		[Test]
		public void TestLoggingWithCustomPrefixes ()
		{
			using (var stream = new MemoryStream ()) {
				using (var logger = new ProtocolLogger (stream, true) { ClientPrefix = "C> ", ServerPrefix = "S> ", RedactSecrets = false }) {
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
					Assert.That (line, Is.EqualTo ("Connected to pop://pop.skyfall.net:110/"));

					line = reader.ReadLine ();
					Assert.That (line, Is.EqualTo ("C> RETR 1"));

					using (var response = GetType ().Assembly.GetManifestResourceStream ("UnitTests.Net.Pop3.Resources.comcast.retr1.txt")) {
						using (var r = new StreamReader (response)) {
							string expected;

							while ((expected = r.ReadLine ()) != null) {
								line = reader.ReadLine ();

								Assert.That (line, Is.EqualTo ("S> " + expected));
							}
						}
					}
				}
			}
		}

		static bool TryExtractTimestamp (ref string text, string format, out DateTime timestamp)
		{
			int index = text.IndexOf (' ');

			if (index == -1) {
				timestamp = default;
				return false;
			}

			var ts = text.Substring (0, index);
			if (!DateTime.TryParseExact (ts, format, CultureInfo.InvariantCulture, DateTimeStyles.None, out timestamp))
				return false;

			text = text.Substring (index + 1);

			return true;
		}

		[Test]
		public void TestLoggingWithTimestamps ()
		{
			string format;

			using (var stream = new MemoryStream ()) {
				using (var logger = new ProtocolLogger (stream, true) { LogTimestamps = true, RedactSecrets = false }) {
					format = logger.TimestampFormat;

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
					DateTime timestamp;
					string line;

					line = reader.ReadLine ();

					Assert.That (TryExtractTimestamp (ref line, format, out timestamp), Is.True, "Connect timestamp");
					Assert.That (line, Is.EqualTo ("Connected to pop://pop.skyfall.net:110/"));

					line = reader.ReadLine ();
					Assert.That (TryExtractTimestamp (ref line, format, out timestamp), Is.True, "C: RETR 1 timestamp");
					Assert.That (line, Is.EqualTo ("C: RETR 1"));

					using (var response = GetType ().Assembly.GetManifestResourceStream ("UnitTests.Net.Pop3.Resources.comcast.retr1.txt")) {
						using (var r = new StreamReader (response)) {
							string expected;

							while ((expected = r.ReadLine ()) != null) {
								line = reader.ReadLine ();

								Assert.That (TryExtractTimestamp (ref line, format, out timestamp), Is.True, "S: timestamp");
								Assert.That (line, Is.EqualTo ("S: " + expected));
							}
						}
					}
				}
			}
		}
	}
}
