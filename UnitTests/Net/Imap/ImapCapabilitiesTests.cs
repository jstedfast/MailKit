//
// ImapCapabilitiesTests.cs
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

using System.Text;
using System.Collections.Generic;

using MailKit.Security;
using MailKit.Net.Imap;

namespace UnitTests.Net.Imap {
	[TestFixture]
	public class ImapCapabilitiesTests
	{
		[Test]
		public void TestArgumentExceptions ()
		{
			Assert.Throws<ArgumentNullException> (() => new ImapCapabilities (null));
			Assert.Throws<ArgumentOutOfRangeException> (() => new ImapCapabilities (new [] { (ImapCapability) (-1) }));
			Assert.Throws<ArgumentOutOfRangeException> (() => new ImapCapabilities (new [] { (ImapCapability) 1000 }));

			var capabilities = new ImapCapabilities (Array.Empty<ImapCapability> ());
			Assert.Throws<ArgumentNullException> (() => capabilities.Contains (null));
			Assert.Throws<ArgumentNullException> (() => capabilities.GetValues (null));
		}

		[Test]
		public void TestCapabilityValuesAreSequential ()
		{
			// ImapCapabilities uses the enum values as bit indexes, so they must be 0, 1, 2, ...
			var values = (int[]) Enum.GetValues (typeof (ImapCapability));

			Array.Sort (values);

			for (int i = 0; i < values.Length; i++)
				Assert.That (values[i], Is.EqualTo (i), "ImapCapability values must be sequential, starting at 0");

			var all = new ImapCapabilities ((ImapCapability[]) Enum.GetValues (typeof (ImapCapability)));

			Assert.That (all, Has.Count.EqualTo (values.Length));
			Assert.That (all, Is.EqualTo (Enum.GetValues (typeof (ImapCapability))));
		}

		[Test]
		public void TestConstructor ()
		{
			var capabilities = new ImapCapabilities (new [] { ImapCapability.GMailExt1, ImapCapability.Idle, ImapCapability.IMAP4rev1 }, new [] { "IMAP4rev1", "IDLE", "X-GM-EXT-1", "AUTH=PLAIN" });

			Assert.That (capabilities, Has.Count.EqualTo (3));
			Assert.That (capabilities, Is.EqualTo (new [] { ImapCapability.IMAP4rev1, ImapCapability.Idle, ImapCapability.GMailExt1 }), "Enumeration order");
			Assert.That (capabilities.Names, Is.EqualTo (new [] { "IMAP4rev1", "IDLE", "X-GM-EXT-1", "AUTH=PLAIN" }));
			Assert.That (capabilities.GetValues ("AUTH"), Is.EqualTo (new [] { "PLAIN" }));
		}

		[Test]
		public void TestDisable ()
		{
			var capabilities = new ImapCapabilities (new [] { ImapCapability.IMAP4rev1, ImapCapability.Idle }, new [] { "IMAP4rev1", "IDLE" });

			Assert.That (capabilities.Disable (ImapCapability.Idle), Is.True);
			Assert.That (capabilities.Disable (ImapCapability.Idle), Is.False);
			Assert.That (capabilities, Is.EqualTo (new [] { ImapCapability.IMAP4rev1 }));
			Assert.That (capabilities.Contains ("IDLE"), Is.True);
		}

		[Test]
		public void TestClientCapabilities ()
		{
			var greeting = "* OK [CAPABILITY IMAP4rev1 IDLE AUTH=PLAIN AUTH=XOAUTH2 auth=plain THREAD=REFERENCES THREAD=ORDEREDSUBJECT X-UNKNOWN-EXT] Ready.\r\n";
			var commands = new List<ImapReplayCommand> {
				new ImapReplayCommand ("", Encoding.ASCII.GetBytes (greeting)),
				new ImapReplayCommand ("A00000000 LOGOUT\r\n", "gmail.logout.txt")
			};

			using (var client = new ImapClient () { TagPrefix = 'A' }) {
				client.Connect (new ImapReplayStream (commands, false), "localhost", 143, SecureSocketOptions.None);

				var capabilities = client.Capabilities;

				Assert.That (capabilities.Names, Is.EqualTo (new [] {
					"IMAP4rev1", "IDLE", "AUTH=PLAIN", "AUTH=XOAUTH2", "THREAD=REFERENCES", "THREAD=ORDEREDSUBJECT", "X-UNKNOWN-EXT"
				}));
				Assert.That (capabilities.Contains ("x-unknown-ext"), Is.True, "Unknown atom");
				Assert.That (capabilities.Contains ("X-OTHER"), Is.False);
				Assert.That (capabilities.GetValues ("AUTH"), Is.EqualTo (new [] { "PLAIN", "XOAUTH2" }));
				Assert.That (capabilities.GetValues ("THREAD"), Is.EqualTo (new [] { "REFERENCES", "ORDEREDSUBJECT" }));
				Assert.That (capabilities.GetValues ("SORT"), Is.Empty);

				Assert.That (capabilities.Contains (ImapCapability.IMAP4rev1), Is.True);
				Assert.That (capabilities.Contains (ImapCapability.Idle), Is.True);
				Assert.That (capabilities.Contains (ImapCapability.Thread), Is.True);

				client.Disconnect (true);
			}
		}
	}
}