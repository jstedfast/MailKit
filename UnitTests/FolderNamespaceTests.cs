//
// FolderNamespaceTests.cs
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
using System.Collections;

using NUnit.Framework;

using MailKit;

namespace UnitTests {
	[TestFixture]
	public class FolderNamespaceTests
	{
		[Test]
		public void TestFolderNamespace ()
		{
			Assert.Throws<ArgumentNullException> (() => new FolderNamespace ('.', null));
		}

		[Test]
		public void TestFolderNamespaceCollection ()
		{
			var namespaces = new FolderNamespaceCollection ();
			FolderNamespace ns;
			int i = 0;

			Assert.Throws<ArgumentNullException> (() => namespaces.Add (null));
			Assert.Throws<ArgumentNullException> (() => namespaces.Contains (null));
			Assert.Throws<ArgumentNullException> (() => namespaces.Remove (null));
			Assert.Throws<ArgumentOutOfRangeException> (() => ns = namespaces[-1]);
			Assert.Throws<ArgumentOutOfRangeException> (() => namespaces[-1] = new FolderNamespace ('.', ""));

			Assert.AreEqual (0, namespaces.Count);

			ns = new FolderNamespace ('.', "");
			namespaces.Add (ns);
			Assert.AreEqual (1, namespaces.Count);
			Assert.IsTrue (namespaces.Contains (ns));
			Assert.Throws<ArgumentNullException> (() => namespaces[0] = null);

			ns = new FolderNamespace ('\\', "");
			namespaces[0] = ns;
			Assert.AreEqual (1, namespaces.Count);
			Assert.IsTrue (namespaces.Contains (ns));

			Assert.IsTrue (namespaces.Remove (ns));
			Assert.AreEqual (0, namespaces.Count);
			Assert.IsFalse (namespaces.Contains (ns));

			namespaces.Add (new FolderNamespace ('.', ""));
			namespaces.Add (new FolderNamespace ('\\', ""));
			foreach (var item in namespaces)
				Assert.AreEqual (namespaces[i++], item);
			i = 0;
			foreach (object item in (IEnumerable) namespaces)
				Assert.AreEqual (namespaces[i++], item);

			Assert.AreEqual ("((\".\" \"\")(\"\\\\\" \"\"))", namespaces.ToString ());
		}
	}
}
