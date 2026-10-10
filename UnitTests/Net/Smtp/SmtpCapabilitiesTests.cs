//
// SmtpCapabilitiesTests.cs
//
// Author: Jeffrey Stedfast <jestedfa@microsoft.com>
//
// Copyright (c) 2013-2026 .NET Foundation and Contributors
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

using System.Collections.Generic;

using MailKit.Security;
using MailKit.Net.Smtp;

namespace UnitTests.Net.Smtp {
	[TestFixture]
	public class SmtpCapabilitiesTests
	{
		[Test]
		public void TestArgumentExceptions ()
		{
			Assert.Throws<ArgumentNullException> (() => new SmtpCapabilities (null));
			Assert.Throws<ArgumentOutOfRangeException> (() => new SmtpCapabilities (new [] { (SmtpCapability) (-1) }));
			Assert.Throws<ArgumentOutOfRangeException> (() => new SmtpCapabilities (new [] { (SmtpCapability) 1000 }));

			var capabilities = new SmtpCapabilities (Array.Empty<SmtpCapability> ());
			Assert.Throws<ArgumentNullException> (() => capabilities.Contains (null));
			Assert.Throws<ArgumentNullException> (() => capabilities.GetValues (null));
		}

		[Test]
		public void TestCapabilityValuesAreSequential ()
		{
			// SmtpCapabilities uses the enum values as bit indexes, so they must be 0, 1, 2, ...
			var values = (int[]) Enum.GetValues (typeof (SmtpCapability));

			Array.Sort (values);

			for (int i = 0; i < values.Length; i++)
				Assert.That (values[i], Is.EqualTo (i), "SmtpCapability values must be sequential, starting at 0");

			var all = new SmtpCapabilities ((SmtpCapability[]) Enum.GetValues (typeof (SmtpCapability)));

			Assert.That (all, Has.Count.EqualTo (values.Length));
			Assert.That (all, Is.EqualTo (Enum.GetValues (typeof (SmtpCapability))));
		}

		[Test]
		public void TestConstructor ()
		{
			var capabilities = new SmtpCapabilities (new [] { SmtpCapability.StartTLS, SmtpCapability.Size, SmtpCapability.StartTLS }, new [] { "SIZE", "STARTTLS", "X-CUSTOM" });

			Assert.That (capabilities, Has.Count.EqualTo (2));
			Assert.That (capabilities, Is.EqualTo (new [] { SmtpCapability.Size, SmtpCapability.StartTLS }), "Enumeration order");
			Assert.That (capabilities.Contains (SmtpCapability.Size), Is.True);
			Assert.That (capabilities.Contains (SmtpCapability.Pipelining), Is.False);
			Assert.That (capabilities.Names, Is.EqualTo (new [] { "SIZE", "STARTTLS", "X-CUSTOM" }));
			Assert.That (capabilities.Contains ("x-custom"), Is.True);
			Assert.That (capabilities.Contains ("PIPELINING"), Is.False);
			Assert.That (capabilities.ToString (), Is.EqualTo ("Size, StartTLS"));

			capabilities = new SmtpCapabilities (Array.Empty<SmtpCapability> ());
			Assert.That (capabilities, Is.Empty);
			Assert.That (capabilities.Names, Is.Empty);
			Assert.That (capabilities.ToString (), Is.EqualTo (string.Empty));
		}

		[Test]
		public void TestDisable ()
		{
			var capabilities = new SmtpCapabilities (new [] { SmtpCapability.Pipelining, SmtpCapability.Chunking }, new [] { "PIPELINING", "CHUNKING" });

			Assert.That (capabilities.Disable (SmtpCapability.Pipelining), Is.True);
			Assert.That (capabilities.Disable (SmtpCapability.Pipelining), Is.False);
			Assert.That (capabilities.Disable (SmtpCapability.Size), Is.False);
			Assert.That (capabilities, Is.EqualTo (new [] { SmtpCapability.Chunking }));
			Assert.That (capabilities.Contains (SmtpCapability.Pipelining), Is.False);

			// disabling a capability does not affect the raw names advertised by the server
			Assert.That (capabilities.Contains ("PIPELINING"), Is.True);
			Assert.That (capabilities.Names, Is.EqualTo (new [] { "PIPELINING", "CHUNKING" }));
		}

		[Test]
		public void TestClientCapabilities ()
		{
			var commands = new List<SmtpReplayCommand> {
				new SmtpReplayCommand ("", "comcast-greeting.txt"),
				new SmtpReplayCommand ("EHLO unit-tests.mimekit.org\r\n", "comcast-ehlo.txt"),
				new SmtpReplayCommand ("QUIT\r\n", "comcast-quit.txt")
			};

			using (var client = new SmtpClient ()) {
				client.LocalDomain = "unit-tests.mimekit.org";

				client.Connect (new SmtpReplayStream (commands, false), "localhost", 25, SecureSocketOptions.None);

				var capabilities = client.Capabilities;

				// Note: the first line of the EHLO response is the server greeting, not a capability
				Assert.That (capabilities.Names, Is.EqualTo (new [] { "HELP", "AUTH", "SIZE", "ENHANCEDSTATUSCODES", "8BITMIME", "STARTTLS" }));
				Assert.That (capabilities.Contains ("help"), Is.True, "Unknown keyword");
				Assert.That (capabilities.Contains ("PIPELINING"), Is.False);
				Assert.That (capabilities.GetValues ("AUTH"), Is.EqualTo (new [] { "LOGIN", "PLAIN" }));
				Assert.That (capabilities.GetValues ("size"), Is.EqualTo (new [] { "36700160" }));
				Assert.That (capabilities.GetValues ("HELP"), Is.Empty);
				Assert.That (capabilities.GetValues ("X-UNKNOWN"), Is.Empty);

				Assert.That (capabilities, Is.EquivalentTo (new [] {
					SmtpCapability.Authentication, SmtpCapability.Size, SmtpCapability.EnhancedStatusCodes,
					SmtpCapability.EightBitMime, SmtpCapability.StartTLS
				}));

				client.Disconnect (true);
			}
		}
	}
}