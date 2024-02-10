//
// ImapImplementationTests.cs
//
// Author: Jeffrey Stedfast <jestedfa@microsoft.com>
//
// Copyright (c) 2013-2024 .NET Foundation and Contributors
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

using MailKit.Net.Imap;

namespace UnitTests.Net.Imap {
	[TestFixture]
	public class ImapImplementationTests
	{
		[Test]
		public void TestImapImplementationProperties ()
		{
			var impl = new ImapImplementation ();

			impl.Address = "50 Church St.";
			Assert.That (impl.Address, Is.EqualTo ("50 Church St."), "Address");

			impl.Arguments = "-p -q";
			Assert.That (impl.Arguments, Is.EqualTo ("-p -q"), "Arguments");

			impl.Command = "mono ./imap.exe";
			Assert.That (impl.Command, Is.EqualTo ("mono ./imap.exe"), "Command");

			impl.Environment = "MONO_GC=sgen";
			Assert.That (impl.Environment, Is.EqualTo ("MONO_GC=sgen"), "Environment");

			impl.Name = "MailKit";
			Assert.That (impl.Name, Is.EqualTo ("MailKit"), "Name");

			impl.OS = "Windows";
			Assert.That (impl.OS, Is.EqualTo ("Windows"), "OS");

			impl.OSVersion = "6.1";
			Assert.That (impl.OSVersion, Is.EqualTo ("6.1"), "OSVersion");

			impl.ReleaseDate = "${Date}";
			Assert.That (impl.ReleaseDate, Is.EqualTo ("${Date}"), "ReleaseDate");

			impl.SupportUrl = "https://github.com/jstedfast/MailKit";
			Assert.That (impl.SupportUrl, Is.EqualTo ("https://github.com/jstedfast/MailKit"), "SupportUrl");

			impl.Vendor = "Microsoft";
			Assert.That (impl.Vendor, Is.EqualTo ("Microsoft"), "Vendor");

			impl.Version = "2.0.7";
			Assert.That (impl.Version, Is.EqualTo ("2.0.7"), "Version");
		}
	}
}
