//
// UriExtensionTests.cs
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
using System.Collections.Generic;

using NUnit.Framework;

using MailKit;

namespace UnitTests {
	[TestFixture]
	public class UriExtensionTests
	{
		[Test]
		public void TestNoQuery ()
		{
			var uri = new Uri ("imap://imap.gmail.com/");
			var query = uri.ParsedQuery ();

			Assert.AreEqual (0, query.Count, "Unexpected number of queries.");
		}

		[Test]
		public void TestSimpleQuery ()
		{
			var uri = new Uri ("imap://imap.gmail.com/?starttls=false");
			var query = uri.ParsedQuery ();

			Assert.AreEqual (1, query.Count, "Unexpected number of queries.");
			Assert.AreEqual ("false", query["starttls"], "Unexpected value for 'starttls'.");
		}

		[Test]
		public void TestCompoundQuery ()
		{
			var uri = new Uri ("imap://imap.gmail.com/?starttls=false&compress=false");
			var query = uri.ParsedQuery ();

			Assert.AreEqual (2, query.Count, "Unexpected number of queries.");
			Assert.AreEqual ("false", query["starttls"], "Unexpected value for 'starttls'.");
			Assert.AreEqual ("false", query["compress"], "Unexpected value for 'compress'.");
		}
	}
}
