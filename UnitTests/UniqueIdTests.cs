//
// UniqueIdTests.cs
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
	public class UniqueIdTests
	{
		[Test]
		public void TestArgumentExceptions ()
		{
			UniqueId uid;

			Assert.Throws<ArgumentOutOfRangeException> (() => new UniqueId (0, 0));
			Assert.Throws<ArgumentOutOfRangeException> (() => new UniqueId (0));

			Assert.Throws<ArgumentNullException> (() => UniqueId.TryParse (null, out uid));
			Assert.Throws<ArgumentNullException> (() => UniqueId.TryParse (null, 0, out uid));
		}

		[Test]
		public void TestEquality ()
		{
			Assert.That (UniqueId.MinValue == UniqueId.MaxValue, Is.False, "MinValue == MaxValue");
			Assert.That (UniqueId.MinValue != UniqueId.MaxValue, Is.True, "MinValue != MaxValue");
			Assert.That (UniqueId.MinValue.Equals (new UniqueId (1)), Is.True, "MinValue.Equals(1)");
			Assert.That (UniqueId.MinValue.Equals ((object) new UniqueId (1)), Is.True, "Boxed MinValue.Equals(1)");
			Assert.That (new UniqueId (1).GetHashCode (), Is.EqualTo (UniqueId.MinValue.GetHashCode ()), "GetHashCode");
			Assert.That (UniqueId.MaxValue, Is.Not.EqualTo (UniqueId.MinValue));
			//Assert.That (new UniqueId (5), Is.EqualTo (new UniqueId (5)));
		}

		[Test]
		public void TestComparisons ()
		{
			Assert.That (new UniqueId (5), Is.LessThanOrEqualTo (new UniqueId (5)), "5 <= 5");
			Assert.That (new UniqueId (1), Is.LessThanOrEqualTo (new UniqueId (5)), "1 <= 5");
			Assert.That (new UniqueId (1), Is.LessThan (new UniqueId (5)), "1 < 5");

			Assert.That (new UniqueId (5), Is.GreaterThanOrEqualTo (new UniqueId (5)), "5 >= 5");
			Assert.That (new UniqueId (5), Is.GreaterThanOrEqualTo (new UniqueId (1)), "5 >= 1");
			Assert.That (new UniqueId (5), Is.GreaterThan (new UniqueId (1)), "5 > 1");

			Assert.That (new UniqueId (1).CompareTo (new UniqueId (5)), Is.EqualTo (-1), "1.CompareTo (5)");
			Assert.That (new UniqueId (5).CompareTo (new UniqueId (1)), Is.EqualTo (1), "5.CompareTo (1)");
			Assert.That (new UniqueId (5).CompareTo (new UniqueId (5)), Is.EqualTo (0), "5.CompareTo (5)");
		}

		[Test]
		public void TestIsValid ()
		{
			Assert.That (UniqueId.Invalid.IsValid, Is.False, "Invalid.IsValid");
			Assert.That (UniqueId.MinValue.IsValid, Is.True, "MinValue.IsValid");
		}

		[Test]
		public void TestToString ()
		{
			Assert.That (UniqueId.MaxValue.ToString (), Is.EqualTo ("4294967295"), "MaxValue");
			Assert.That (UniqueId.MinValue.ToString (), Is.EqualTo ("1"), "MinValue");
		}

		[Test]
		public void TestParsing ()
		{
			UniqueId uid;
			int index = 0;

			// make sure that parsing bad input fails
			Assert.That (UniqueId.TryParse ("text", ref index, out _), Is.False, "text");
			Assert.That (UniqueId.TryParse ("text", 20160117, out _), Is.False, "text");
			Assert.That (UniqueId.TryParse ("text", out _), Is.False, "text");

			// make sure that parsing uint.MaxValue works
			index = 0;
			Assert.That (UniqueId.TryParse ("4294967295", ref index, out _), Is.True, "4294967295");
			Assert.That (UniqueId.TryParse ("4294967295", 20160117, out uid), Is.True, "4294967295");
			Assert.That (uid.Validity, Is.EqualTo (20160117));
			Assert.That (uid, Is.EqualTo (UniqueId.MaxValue));

			Assert.That (UniqueId.TryParse ("4294967295", out uid), Is.True, "4294967295");
			Assert.That (uid, Is.EqualTo (UniqueId.MaxValue));

			uid = UniqueId.Parse ("4294967295", 20160117);
			Assert.That (uid.Validity, Is.EqualTo (20160117));
			Assert.That (uid, Is.EqualTo (UniqueId.MaxValue));

			uid = UniqueId.Parse ("4294967295");
			Assert.That (uid, Is.EqualTo (UniqueId.MaxValue));

			// make sure parsing a value larger than uint.MaxValue fails
			index = 0;
			Assert.That (UniqueId.TryParse ("4294967296", ref index, out _), Is.False, "4294967296");

			index = 0;
			Assert.That (UniqueId.TryParse ("4294967305", ref index, out _), Is.False, "4294967305");
		}
	}
}
