//
// PartialRangeTests.cs
//
// Author: Andrii Martyniuk <pretosync@gmail.com>
//
// Copyright (c) 2013-2026 .NET Foundation and Contributors
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

using System.Globalization;

using MailKit;

namespace UnitTests {
	[TestFixture]
	public class PartialRangeTests
	{
		[Test]
		public void TestArgumentExceptions ()
		{
			Assert.Throws<ArgumentOutOfRangeException> (() => new PartialRange (0, 5), "first is zero");
			Assert.Throws<ArgumentOutOfRangeException> (() => new PartialRange (5, 0), "last is zero");
			Assert.Throws<ArgumentOutOfRangeException> (() => new PartialRange (-1, 5), "mixed signs (negative first)");
			Assert.Throws<ArgumentOutOfRangeException> (() => new PartialRange (1, -5), "mixed signs (negative last)");
			Assert.Throws<ArgumentOutOfRangeException> (() => new PartialRange (4294967296L, 4294967297L), "first > uint.MaxValue");
			Assert.Throws<ArgumentOutOfRangeException> (() => new PartialRange (1, 4294967296L), "last > uint.MaxValue");
			Assert.Throws<ArgumentOutOfRangeException> (() => new PartialRange (-4294967296L, -1), "first < -uint.MaxValue");
			Assert.Throws<ArgumentOutOfRangeException> (() => new PartialRange (-1, -4294967296L), "last < -uint.MaxValue");
		}

		[Test]
		public void TestConstructor ()
		{
			var range = new PartialRange (1, 100);
			Assert.That (range.First, Is.EqualTo (1), "First");
			Assert.That (range.Last, Is.EqualTo (100), "Last");

			range = new PartialRange (-1, -100);
			Assert.That (range.First, Is.EqualTo (-1), "First (negative)");
			Assert.That (range.Last, Is.EqualTo (-100), "Last (negative)");

			range = new PartialRange (7, 7);
			Assert.That (range.First, Is.EqualTo (7), "First (single)");
			Assert.That (range.Last, Is.EqualTo (7), "Last (single)");

			range = new PartialRange (uint.MaxValue, uint.MaxValue);
			Assert.That (range.First, Is.EqualTo (4294967295L), "First (max)");
			Assert.That (range.Last, Is.EqualTo (4294967295L), "Last (max)");
		}

		[Test]
		public void TestToString ()
		{
			Assert.That (new PartialRange (1, 100).ToString (), Is.EqualTo ("1:100"));
			Assert.That (new PartialRange (500, 400).ToString (), Is.EqualTo ("500:400"));
			Assert.That (new PartialRange (-1, -100).ToString (), Is.EqualTo ("-1:-100"));
		}

		[Test]
		public void TestToStringIsCultureInvariant ()
		{
			var culture = Thread.CurrentThread.CurrentCulture;

			try {
				Thread.CurrentThread.CurrentCulture = CultureInfo.GetCultureInfo ("de-DE");

				Assert.That (new PartialRange (-1, -100).ToString (), Is.EqualTo ("-1:-100"));
			} finally {
				Thread.CurrentThread.CurrentCulture = culture;
			}
		}

		[Test]
		public void TestTryParse ()
		{
			Assert.That (PartialRange.TryParse ("1:100", out var range), Is.True, "1:100");
			Assert.That (range, Is.EqualTo (new PartialRange (1, 100)), "1:100 value");

			Assert.That (PartialRange.TryParse ("-1:-100", out range), Is.True, "-1:-100");
			Assert.That (range, Is.EqualTo (new PartialRange (-1, -100)), "-1:-100 value");

			Assert.That (PartialRange.TryParse ("500:500", out range), Is.True, "500:500");
			Assert.That (range, Is.EqualTo (new PartialRange (500, 500)), "500:500 value");

			Assert.That (PartialRange.TryParse ("4294967295:4294967295", out range), Is.True, "4294967295:4294967295");
			Assert.That (range, Is.EqualTo (new PartialRange (4294967295L, 4294967295L)), "4294967295:4294967295 value");
		}

		[Test]
		public void TestTryParseInvalid ()
		{
			var invalid = new string[] {
				null, "", "NIL", "nil", "*", "1:*", "0:5", "5:0", "-1:5", "1:-5", "1", ":5", "5:", "1:2:3",
				"4294967296:4294967297", "-4294967296:-1", "abc:def", "1:100 ", " 1:100", "1.5:2.5"
			};

			foreach (var text in invalid)
				Assert.That (PartialRange.TryParse (text, out _), Is.False, $"TryParse (\"{text}\")");
		}

		[Test]
		public void TestEquality ()
		{
			var range = new PartialRange (1, 100);

			Assert.That (range.Equals (new PartialRange (1, 100)), Is.True, "Equals");
			Assert.That (range.Equals ((object) new PartialRange (1, 100)), Is.True, "Equals (object)");
			Assert.That (range.Equals (new PartialRange (1, 101)), Is.False, "Equals (different Last)");
			Assert.That (range.Equals (new PartialRange (2, 100)), Is.False, "Equals (different First)");
			Assert.That (range.Equals (new PartialRange (-1, -100)), Is.False, "Equals (negative)");
			Assert.That (range.Equals ("1:100"), Is.False, "Equals (string)");

			Assert.That (range == new PartialRange (1, 100), Is.True, "operator ==");
			Assert.That (range != new PartialRange (1, 101), Is.True, "operator !=");

			Assert.That (range.GetHashCode (), Is.EqualTo (new PartialRange (1, 100).GetHashCode ()), "GetHashCode");
		}
	}
}
