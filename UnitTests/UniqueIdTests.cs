//
// UniqueIdTests.cs
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

using MailKit;

namespace UnitTests {
	[TestFixture]
	public class UniqueIdTests
	{
		[Test]
		public void TestArgumentExceptions ()
		{
			Assert.Throws<ArgumentOutOfRangeException> (() => new UniqueId (0, 0));
			Assert.Throws<ArgumentOutOfRangeException> (() => new UniqueId (0));
		}

		[Test]
		public void TestEquality ()
		{
			Assert.IsFalse (UniqueId.MinValue == UniqueId.MaxValue, "MinValue == MaxValue");
			Assert.IsTrue (UniqueId.MinValue != UniqueId.MaxValue, "MinValue != MaxValue");
			Assert.IsTrue (UniqueId.MinValue.Equals (new UniqueId (1)), "MinValue.Equals(1)");
			Assert.AreNotEqual (UniqueId.MinValue, UniqueId.MaxValue);
			Assert.AreEqual (new UniqueId (5), new UniqueId (5));
		}

		[Test]
		public void TestComparisons ()
		{
			Assert.IsTrue (new UniqueId (5) <= new UniqueId (5), "5 <= 5");
			Assert.IsTrue (new UniqueId (1) <= new UniqueId (5), "1 <= 5");
			Assert.IsTrue (new UniqueId (1) < new UniqueId (5), "1 < 5");

			Assert.IsTrue (new UniqueId (5) >= new UniqueId (5), "5 >= 5");
			Assert.IsTrue (new UniqueId (5) >= new UniqueId (1), "5 >= 1");
			Assert.IsTrue (new UniqueId (5) > new UniqueId (1), "5 > 1");

			Assert.AreEqual (-1, new UniqueId (1).CompareTo (new UniqueId (5)), "1.CompareTo (5)");
			Assert.AreEqual (1, new UniqueId (5).CompareTo (new UniqueId (1)), "5.CompareTo (1)");
			Assert.AreEqual (0, new UniqueId (5).CompareTo (new UniqueId (5)), "5.CompareTo (5)");
		}

		[Test]
		public void TestIsValid ()
		{
			Assert.IsFalse (UniqueId.Invalid.IsValid, "Invalid.IsValid");
			Assert.IsTrue (UniqueId.MinValue.IsValid, "MinValue.IsValid");
		}

		[Test]
		public void TestToString ()
		{
			Assert.AreEqual ("4294967295", UniqueId.MaxValue.ToString (), "MaxValue");
			Assert.AreEqual ("1", UniqueId.MinValue.ToString (), "MinValue");
		}

		[Test]
		public void TestParsing ()
		{
			UniqueId uid;
			int index = 0;
			uint u;

			// make sure that parsing bad input fails
			Assert.IsFalse (UniqueId.TryParse ("text", ref index, out u), "text");
			Assert.IsFalse (UniqueId.TryParse ("text", 20160117, out uid), "text");
			Assert.IsFalse (UniqueId.TryParse ("text", out uid), "text");

			// make sure that parsing uint.MaxValue works
			index = 0;
			Assert.IsTrue (UniqueId.TryParse ("4294967295", ref index, out u), "4294967295");
			Assert.IsTrue (UniqueId.TryParse ("4294967295", 20160117, out uid), "4294967295");
			Assert.AreEqual (20160117, uid.Validity);
			Assert.AreEqual (UniqueId.MaxValue, uid);

			Assert.IsTrue (UniqueId.TryParse ("4294967295", out uid), "4294967295");
			Assert.AreEqual (UniqueId.MaxValue, uid);

			uid = UniqueId.Parse ("4294967295", 20160117);
			Assert.AreEqual (20160117, uid.Validity);
			Assert.AreEqual (UniqueId.MaxValue, uid);

			uid = UniqueId.Parse ("4294967295");
			Assert.AreEqual (UniqueId.MaxValue, uid);

			// make sure parsing a value larger than uint.MaxValue fails
			index = 0;
			Assert.IsFalse (UniqueId.TryParse ("4294967296", ref index, out u), "4294967296");

			index = 0;
			Assert.IsFalse (UniqueId.TryParse ("4294967305", ref index, out u), "4294967305");
		}
	}
}
