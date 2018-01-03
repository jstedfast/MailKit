//
// UniqueIdRangeTests.cs
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
	public class UniqueIdRangeTests
	{
		[Test]
		public void TestArgumentExceptions ()
		{
			var uids = new UniqueIdRange (UniqueId.MinValue, UniqueId.MinValue);
			UniqueId uid;

			Assert.Throws<ArgumentOutOfRangeException> (() => new UniqueIdRange (UniqueId.Invalid, UniqueId.MaxValue));
			Assert.Throws<ArgumentOutOfRangeException> (() => new UniqueIdRange (UniqueId.MinValue, UniqueId.Invalid));

			Assert.Throws<ArgumentOutOfRangeException> (() => new UniqueIdRange (0, 0, 1));
			Assert.Throws<ArgumentOutOfRangeException> (() => new UniqueIdRange (0, 1, 0));

			Assert.Throws<ArgumentNullException> (() => uids.CopyTo (null, 0));
			Assert.Throws<ArgumentOutOfRangeException> (() => uids.CopyTo (new UniqueId[1], -1));
			Assert.Throws<ArgumentOutOfRangeException> (() => uid = uids[-1]);
			Assert.Throws<ArgumentNullException> (() => UniqueIdRange.TryParse (null, 0, out uids));
		}

		[Test]
		public void TestAscending ()
		{
			const string example = "1:20";
			var copy = new UniqueId[20];
			UniqueIdRange uids;

			Assert.IsTrue (UniqueIdRange.TryParse (example, 20160117, out uids), "Failed to parse uids.");
			Assert.AreEqual (20160117, uids.Validity, "Validity");
			Assert.IsTrue (uids.IsReadOnly, "IsReadOnly");
			Assert.AreEqual (1, uids.Start.Id, "Start");
			Assert.AreEqual (20, uids.End.Id, "End");
			Assert.AreEqual (1, uids.Min.Id, "Min");
			Assert.AreEqual (20, uids.Max.Id, "Max");
			Assert.AreEqual (example, uids.ToString (), "ToString");
			Assert.AreEqual (20, uids.Count);

			Assert.False (uids.Contains (new UniqueId (500)));
			Assert.AreEqual (-1, uids.IndexOf (new UniqueId (500)));

			for (int i = 0; i < uids.Count; i++) {
				Assert.AreEqual (i, uids.IndexOf (uids[i]));
				Assert.AreEqual (20160117, uids[i].Validity);
				Assert.AreEqual (i + 1, uids[i].Id);
			}

			uids.CopyTo (copy, 0);

			for (int i = 0; i < copy.Length; i++) {
				Assert.AreEqual (20160117, copy[i].Validity);
				Assert.AreEqual (i + 1, copy[i].Id);
			}

			var list = new List<UniqueId> ();
			foreach (var uid in uids) {
				Assert.AreEqual (20160117, uid.Validity);
				list.Add (uid);
			}

			for (int i = 0; i < list.Count; i++)
				Assert.AreEqual (i + 1, list[i].Id);
		}

		[Test]
		public void TestDescending ()
		{
			const string example = "20:1";
			var copy = new UniqueId[20];
			UniqueIdRange uids;

			Assert.IsTrue (UniqueIdRange.TryParse (example, 20160117, out uids), "Failed to parse uids.");
			Assert.AreEqual (20160117, uids.Validity, "Validity");
			Assert.IsTrue (uids.IsReadOnly, "IsReadOnly");
			Assert.AreEqual (20, uids.Start.Id, "Start");
			Assert.AreEqual (1, uids.End.Id, "End");
			Assert.AreEqual (1, uids.Min.Id, "Min");
			Assert.AreEqual (20, uids.Max.Id, "Max");
			Assert.AreEqual (example, uids.ToString (), "ToString");
			Assert.AreEqual (20, uids.Count);

			Assert.False (uids.Contains (new UniqueId (500)));
			Assert.AreEqual (-1, uids.IndexOf (new UniqueId (500)));

			for (int i = 0; i < uids.Count; i++) {
				Assert.AreEqual (i, uids.IndexOf (uids[i]));
				Assert.AreEqual (20160117, uids[i].Validity);
				Assert.AreEqual (20 - i, uids[i].Id);
			}

			uids.CopyTo (copy, 0);

			for (int i = 0; i < copy.Length; i++) {
				Assert.AreEqual (20160117, copy[i].Validity);
				Assert.AreEqual (20 - i, copy[i].Id);
			}

			var list = new List<UniqueId> ();
			foreach (var uid in uids) {
				Assert.AreEqual (20160117, uid.Validity);
				list.Add (uid);
			}

			for (int i = 0; i < list.Count; i++)
				Assert.AreEqual (20 - i, list[i].Id);
		}

		[Test]
		public void TestNotSupported ()
		{
			var range = new UniqueIdRange (UniqueId.MinValue, UniqueId.MaxValue);

			Assert.Throws<NotSupportedException> (() => range[5] = new UniqueId (5), "set");
			Assert.Throws<NotSupportedException> (() => range.Insert (0, new UniqueId (5)), "Insert");
			Assert.Throws<NotSupportedException> (() => range.Remove (new UniqueId (5)), "Remove");
			Assert.Throws<NotSupportedException> (() => range.RemoveAt (1), "RemoveAt");
			Assert.Throws<NotSupportedException> (() => range.Add (new UniqueId (5)), "Add");
			Assert.Throws<NotSupportedException> (() => range.Clear (), "Clear");
		}

		[Test]
		public void TestParser ()
		{
			UniqueIdRange range;

			Assert.IsFalse (UniqueIdRange.TryParse ("xyz", out range));
			Assert.IsFalse (UniqueIdRange.TryParse ("1:xyz", out range));
			Assert.IsFalse (UniqueIdRange.TryParse ("1:*1", out range));
			Assert.IsFalse (UniqueIdRange.TryParse ("1:1x", out range));

			Assert.IsTrue (UniqueIdRange.TryParse ("1:*", out range));
			Assert.AreEqual (UniqueId.MinValue, range.Min);
			Assert.AreEqual (UniqueId.MaxValue, range.Max);
		}
	}
}
