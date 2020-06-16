//
// MetadataTests.cs
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

using NUnit.Framework;

using MailKit;

namespace UnitTests {
	[TestFixture]
	public class MetadataTests
	{
		[Test]
		public void TestMetadataTag ()
		{
			Assert.Throws<ArgumentNullException> (() => new MetadataTag (null));
			Assert.Throws<ArgumentException> (() => new MetadataTag (string.Empty));

			var tag1 = new MetadataTag ("/dev/null");
			var tag2 = new MetadataTag ("/dev/null");
			var tag3 = new MetadataTag ("/opt/nope");

			Assert.IsTrue (tag1.Equals (tag2), "Equals #1");
			Assert.IsFalse (tag1.Equals (tag3), "Equals #2");
			Assert.AreEqual (tag1.GetHashCode (), tag2.GetHashCode (), "GetHashCode #1");
			Assert.AreNotEqual (tag1.GetHashCode (), tag3.GetHashCode (), "GetHashCode #2");

			Assert.AreEqual (MetadataTag.PrivateComment, MetadataTag.Create (MetadataTag.PrivateComment.ToString ()));
			Assert.AreEqual (MetadataTag.PrivateSpecialUse, MetadataTag.Create (MetadataTag.PrivateSpecialUse.ToString ()));
			Assert.AreEqual (MetadataTag.SharedAdmin, MetadataTag.Create (MetadataTag.SharedAdmin.ToString ()));
			Assert.AreEqual (MetadataTag.SharedComment, MetadataTag.Create (MetadataTag.SharedComment.ToString ()));
			Assert.AreEqual (tag1, MetadataTag.Create (tag1.Id));
		}

		[Test]
		public void TestMetadataOptions ()
		{
			var options = new MetadataOptions ();

			Assert.AreEqual (0, options.Depth);
			Assert.AreEqual (0, options.LongEntries);
			Assert.IsNull (options.MaxSize);

			Assert.Throws<ArgumentOutOfRangeException> (() => options.Depth = 500);
		}

		[Test]
		public void TestMetadataCollection ()
		{
			Assert.Throws<ArgumentNullException> (() => new MetadataCollection (null));
		}
	}
}
