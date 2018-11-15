//
// ImapEngineTests.cs
//
// Author: Jeffrey Stedfast <jestedfa@microsoft.com>
//
// Copyright (c) 2013-2018 Xamarin Inc. (www.xamarin.com)
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

using NUnit.Framework;

using MailKit.Net.Imap;

namespace UnitTests.Net.Imap {
	[TestFixture]
	public class ImapEngineTests
	{
		[Test]
		public void TestGetResponseCodeType ()
		{
			foreach (ImapResponseCodeType type in Enum.GetValues (typeof (ImapResponseCodeType))) {
				string atom;

				switch (type) {
				case ImapResponseCodeType.ReadOnly: atom = "READ-ONLY"; break;
				case ImapResponseCodeType.ReadWrite: atom = "READ-WRITE"; break;
				case ImapResponseCodeType.UnknownCte: atom = "UNKNOWN-CTE"; break;
				case ImapResponseCodeType.UndefinedFilter: atom = "UNDEFINED-FILTER"; break;
				default: atom = type.ToString ().ToUpperInvariant (); break;
				}

				var result = ImapEngine.GetResponseCodeType (atom);
				Assert.AreEqual (type, result);
			}
		}
	}
}
