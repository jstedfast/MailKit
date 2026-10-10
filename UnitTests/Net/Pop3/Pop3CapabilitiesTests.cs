//
// Pop3CapabilitiesTests.cs
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
using MailKit.Net.Pop3;

namespace UnitTests.Net.Pop3 {
	[TestFixture]
	public class Pop3CapabilitiesTests
	{
		[Test]
		public void TestArgumentExceptions ()
		{
			Assert.Throws<ArgumentNullException> (() => new Pop3Capabilities (null));
			Assert.Throws<ArgumentOutOfRangeException> (() => new Pop3Capabilities (new [] { (Pop3Capability) (-1) }));
			Assert.Throws<ArgumentOutOfRangeException> (() => new Pop3Capabilities (new [] { (Pop3Capability) 1000 }));

			var capabilities = new Pop3Capabilities (Array.Empty<Pop3Capability> ());
			Assert.Throws<ArgumentNullException> (() => capabilities.Contains (null));
			Assert.Throws<ArgumentNullException> (() => capabilities.GetValues (null));
		}

		[Test]
		public void TestCapabilityValuesAreSequential ()
		{
			// Pop3Capabilities uses the enum values as bit indexes, so they must be 0, 1, 2, ...
			var values = (int[]) Enum.GetValues (typeof (Pop3Capability));

			Array.Sort (values);

			for (int i = 0; i < values.Length; i++)
				Assert.That (values[i], Is.EqualTo (i), "Pop3Capability values must be sequential, starting at 0");

			var all = new Pop3Capabilities ((Pop3Capability[]) Enum.GetValues (typeof (Pop3Capability)));

			Assert.That (all, Has.Count.EqualTo (values.Length));
			Assert.That (all, Is.EqualTo (Enum.GetValues (typeof (Pop3Capability))));
		}

		[Test]
		public void TestConstructor ()
		{
			var capabilities = new Pop3Capabilities (new [] { Pop3Capability.UIDL, Pop3Capability.Top }, new [] { "TOP", "UIDL", "X-CUSTOM" });

			Assert.That (capabilities, Has.Count.EqualTo (2));
			Assert.That (capabilities, Is.EqualTo (new [] { Pop3Capability.Top, Pop3Capability.UIDL }), "Enumeration order");
			Assert.That (capabilities.Names, Is.EqualTo (new [] { "TOP", "UIDL", "X-CUSTOM" }));
			Assert.That (capabilities.Contains ("x-custom"), Is.True);
		}

		[Test]
		public void TestDisable ()
		{
			var capabilities = new Pop3Capabilities (new [] { Pop3Capability.Top, Pop3Capability.UIDL }, new [] { "TOP", "UIDL" });

			Assert.That (capabilities.Disable (Pop3Capability.UIDL), Is.True);
			Assert.That (capabilities.Disable (Pop3Capability.UIDL), Is.False);
			Assert.That (capabilities, Is.EqualTo (new [] { Pop3Capability.Top }));
			Assert.That (capabilities.Contains ("UIDL"), Is.True);
		}

		[Test]
		public void TestClientCapabilities ()
		{
			var commands = new List<Pop3ReplayCommand> {
				new Pop3ReplayCommand ("", "comcast.greeting.txt"),
				new Pop3ReplayCommand ("CAPA\r\n", "comcast.capa2.txt"),
				new Pop3ReplayCommand ("QUIT\r\n", "comcast.quit.txt")
			};

			using (var client = new Pop3Client ()) {
				client.Connect (new Pop3ReplayStream (commands, false), "localhost", 110, SecureSocketOptions.None);

				var capabilities = client.Capabilities;

				Assert.That (capabilities.Names, Is.EqualTo (new [] { "TOP", "USER", "UIDL", "STLS", "SASL", "EXPIRE", "XOIP", "IMPLEMENTATION" }));
				Assert.That (capabilities.Contains ("xoip"), Is.True, "Unknown keyword");
				Assert.That (capabilities.GetValues ("SASL"), Is.EqualTo (new [] { "PLAIN", "X-ZIMBRA" }));
				Assert.That (capabilities.GetValues ("EXPIRE"), Is.EqualTo (new [] { "NEVER" }));
				Assert.That (capabilities.GetValues ("IMPLEMENTATION"), Is.EqualTo (new [] { "ZimbraInc" }));
				Assert.That (capabilities.GetValues ("TOP"), Is.Empty);

				Assert.That (capabilities.Contains (Pop3Capability.Top), Is.True);
				Assert.That (capabilities.Contains (Pop3Capability.UIDL), Is.True);
				Assert.That (capabilities.Contains (Pop3Capability.Sasl), Is.True);
				Assert.That (capabilities.Contains (Pop3Capability.StartTLS), Is.True);

				client.Disconnect (true);
			}
		}
	}
}