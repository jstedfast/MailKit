//
// ImapImplementationTests.cs
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

using NUnit.Framework;

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
			Assert.AreEqual ("50 Church St.", impl.Address, "Address");

			impl.Arguments = "-p -q";
			Assert.AreEqual ("-p -q", impl.Arguments, "Arguments");

			impl.Command = "mono ./imap.exe";
			Assert.AreEqual ("mono ./imap.exe", impl.Command, "Command");

			impl.Environment = "MONO_GC=sgen";
			Assert.AreEqual ("MONO_GC=sgen", impl.Environment, "Environment");

			impl.Name = "MailKit";
			Assert.AreEqual ("MailKit", impl.Name, "Name");

			impl.OS = "Windows";
			Assert.AreEqual ("Windows", impl.OS, "OS");

			impl.OSVersion = "6.1";
			Assert.AreEqual ("6.1", impl.OSVersion, "OSVersion");

			impl.ReleaseDate = "${Date}";
			Assert.AreEqual ("${Date}", impl.ReleaseDate, "ReleaseDate");

			impl.SupportUrl = "https://github.com/jstedfast/MailKit";
			Assert.AreEqual ("https://github.com/jstedfast/MailKit", impl.SupportUrl, "SupportUrl");

			impl.Vendor = "Microsoft";
			Assert.AreEqual ("Microsoft", impl.Vendor, "Vendor");

			impl.Version = "2.0.7";
			Assert.AreEqual ("2.0.7", impl.Version, "Version");
		}
	}
}
