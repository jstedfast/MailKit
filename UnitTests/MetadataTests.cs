//
// MetadataTests.cs
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

			Assert.That (tag1.Equals (tag2), Is.True, "Equals #1");
			Assert.That (tag1.Equals (tag3), Is.False, "Equals #2");
			Assert.That (tag2.GetHashCode (), Is.EqualTo (tag1.GetHashCode ()), "GetHashCode #1");
			Assert.That (tag3.GetHashCode (), Is.Not.EqualTo (tag1.GetHashCode ()), "GetHashCode #2");

			Assert.That (MetadataTag.Create (MetadataTag.PrivateComment.ToString ()), Is.EqualTo (MetadataTag.PrivateComment));
			Assert.That (MetadataTag.Create (MetadataTag.PrivateSpecialUse.ToString ()), Is.EqualTo (MetadataTag.PrivateSpecialUse));
			Assert.That (MetadataTag.Create (MetadataTag.SharedAdmin.ToString ()), Is.EqualTo (MetadataTag.SharedAdmin));
			Assert.That (MetadataTag.Create (MetadataTag.SharedComment.ToString ()), Is.EqualTo (MetadataTag.SharedComment));
			Assert.That (MetadataTag.Create (tag1.Id), Is.EqualTo (tag1));
		}

		[Test]
		public void TestMetadataOptions ()
		{
			var options = new MetadataOptions ();

			Assert.That (options.Depth, Is.EqualTo (0));
			Assert.That (options.LongEntries, Is.EqualTo (0));
			Assert.That (options.MaxSize, Is.Null);

			Assert.Throws<ArgumentOutOfRangeException> (() => options.Depth = 500);
		}

		[Test]
		public void TestMetadataCollection ()
		{
			Assert.Throws<ArgumentNullException> (() => new MetadataCollection (null));
		}
	}
}
