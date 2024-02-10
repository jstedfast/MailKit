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

using System.Collections;

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

			Assert.That (headers.Add ("From"), Is.True, "Adding From");
			Assert.That (headers.Add ("From"), Is.False, "Adding From duplicate #1");
			Assert.That (headers.Add ("FROM"), Is.False, "Adding From duplicate #2");
			Assert.That (headers.Add ("fRoM"), Is.False, "Adding From duplicate #3");
			Assert.That (headers.Add (HeaderId.From), Is.False, "Adding From duplicate #4");
			Assert.That (headers, Has.Count.EqualTo (1), "Count #1");

			Assert.That (headers.Remove (HeaderId.From), Is.True, "Removing From");
			Assert.That (headers.Remove ("From"), Is.False, "Removing From duplicate #1");
			Assert.That (headers.Remove (HeaderId.From), Is.False, "Removing From duplicate #2");
			Assert.That (headers, Is.Empty, "Count #2");

			headers.AddRange (new HeaderId[] { HeaderId.Sender, HeaderId.From, HeaderId.ReplyTo });
			Assert.That (headers, Has.Count.EqualTo (3), "Count #3");

			headers.AddRange (new string[] { "to", "cc", "bcc" });
			Assert.That (headers, Has.Count.EqualTo (6), "Count #4");

			Assert.That (headers.Contains (HeaderId.To), Is.True, "Contains #1");
			Assert.That (headers.Contains ("reply-to"), Is.True, "Contains #2");

			var results = new string[headers.Count];
			headers.CopyTo (results, 0);
			Array.Sort (results);
			Assert.That (results[0], Is.EqualTo ("BCC"));
			Assert.That (results[1], Is.EqualTo ("CC"));
			Assert.That (results[2], Is.EqualTo ("FROM"));
			Assert.That (results[3], Is.EqualTo ("REPLY-TO"));
			Assert.That (results[4], Is.EqualTo ("SENDER"));
			Assert.That (results[5], Is.EqualTo ("TO"));

			foreach (var header in headers)
				Assert.That (results, Does.Contain (header));

			foreach (string header in ((IEnumerable) headers))
				Assert.That (results, Does.Contain (header));

			headers.Clear ();
			Assert.That (headers, Is.Empty, "Count after Clear");
		}
	}
}
