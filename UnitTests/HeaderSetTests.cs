//
// HeaderSetTests.cs
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
using System.Linq;
using System.Collections;
using System.Collections.Generic;

using NUnit.Framework;

using MimeKit;
using MailKit;

namespace UnitTests {
	[TestFixture]
	public class HeaderSetTests
	{
		[Test]
		public void TestArgumentExceptions ()
		{
			var headers = new HeaderSet ();
			var array = new string[10];

			Assert.Throws<ArgumentOutOfRangeException> (() => headers.Add (HeaderId.Unknown));
			Assert.Throws<ArgumentNullException> (() => headers.AddRange ((IEnumerable<HeaderId>) null));
			Assert.Throws<ArgumentException> (() => headers.AddRange (new HeaderId[] { HeaderId.Unknown }));

			Assert.Throws<ArgumentNullException> (() => headers.Add (null));
			Assert.Throws<ArgumentException> (() => headers.Add (string.Empty));
			Assert.Throws<ArgumentException> (() => headers.Add ("This is invalid"));
			Assert.Throws<ArgumentNullException> (() => headers.AddRange ((IEnumerable<string>) null));
			Assert.Throws<ArgumentException> (() => headers.AddRange (new string[] { "This is invalid" }));

			Assert.Throws<ArgumentNullException> (() => ((ICollection<string>) headers).Add (null));
			Assert.Throws<ArgumentException> (() => ((ICollection<string>) headers).Add (string.Empty));
			Assert.Throws<ArgumentException> (() => ((ICollection<string>) headers).Add ("This is invalid"));

			Assert.Throws<ArgumentNullException> (() => headers.CopyTo (null, 0));
			Assert.Throws<ArgumentOutOfRangeException> (() => headers.CopyTo (array, -1));
			Assert.Throws<ArgumentException> (() => headers.CopyTo (array, 11));

			Assert.Throws<ArgumentOutOfRangeException> (() => headers.Contains (HeaderId.Unknown));
			Assert.Throws<ArgumentNullException> (() => headers.Contains (null));

			Assert.Throws<ArgumentOutOfRangeException> (() => headers.Remove (HeaderId.Unknown));
			Assert.Throws<ArgumentNullException> (() => headers.Remove (null));
		}

		[Test]
		public void TestBasicFunctionality ()
		{
			var headers = new HeaderSet ();

			Assert.IsTrue (headers.Add ("From"), "Adding From");
			Assert.IsFalse (headers.Add ("From"), "Adding From duplicate #1");
			Assert.IsFalse (headers.Add ("FROM"), "Adding From duplicate #2");
			Assert.IsFalse (headers.Add ("fRoM"), "Adding From duplicate #3");
			Assert.IsFalse (headers.Add (HeaderId.From), "Adding From duplicate #4");
			Assert.AreEqual (1, headers.Count, "Count #1");

			Assert.IsTrue (headers.Remove (HeaderId.From), "Removing From");
			Assert.IsFalse (headers.Remove ("From"), "Removing From duplicate #1");
			Assert.IsFalse (headers.Remove (HeaderId.From), "Removing From duplicate #2");
			Assert.AreEqual (0, headers.Count, "Count #2");

			headers.AddRange (new HeaderId[] { HeaderId.Sender, HeaderId.From, HeaderId.ReplyTo });
			Assert.AreEqual (3, headers.Count, "Count #3");

			headers.AddRange (new string[] { "to", "cc", "bcc" });
			Assert.AreEqual (6, headers.Count, "Count #4");

			Assert.IsTrue (headers.Contains (HeaderId.To), "Contains #1");
			Assert.IsTrue (headers.Contains ("reply-to"), "Contains #2");

			var results = new string[headers.Count];
			headers.CopyTo (results, 0);
			Array.Sort (results);
			Assert.AreEqual ("BCC", results[0]);
			Assert.AreEqual ("CC", results[1]);
			Assert.AreEqual ("FROM", results[2]);
			Assert.AreEqual ("REPLY-TO", results[3]);
			Assert.AreEqual ("SENDER", results[4]);
			Assert.AreEqual ("TO", results[5]);

			foreach (var header in headers)
				Assert.IsTrue (results.Contains (header));

			foreach (string header in ((IEnumerable) headers))
				Assert.IsTrue (results.Contains (header));

			headers.Clear ();
			Assert.AreEqual (0, headers.Count, "Count after Clear");
		}
	}
}
