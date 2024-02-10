//
// UniqueIdRangeTests.cs
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

			Assert.That (UniqueIdRange.TryParse (example, 20160117, out uids), Is.True, "Failed to parse uids.");
			Assert.That (uids.Validity, Is.EqualTo (20160117), "Validity");
			Assert.That (uids.IsReadOnly, Is.True, "IsReadOnly");
			Assert.That (uids.Start.Id, Is.EqualTo (1), "Start");
			Assert.That (uids.End.Id, Is.EqualTo (20), "End");
			Assert.That (uids.Min.Id, Is.EqualTo (1), "Min");
			Assert.That (uids.Max.Id, Is.EqualTo (20), "Max");
			Assert.That (uids.ToString (), Is.EqualTo (example), "ToString");
			Assert.That (uids, Has.Count.EqualTo (20));

			Assert.That (uids, Does.Not.Contain (new UniqueId (500)));
			Assert.That (uids.IndexOf (new UniqueId (500)), Is.EqualTo (-1));

			for (int i = 0; i < uids.Count; i++) {
				Assert.That (uids.IndexOf (uids[i]), Is.EqualTo (i));
				Assert.That (uids[i].Validity, Is.EqualTo (20160117));
				Assert.That (uids[i].Id, Is.EqualTo (i + 1));
			}

			uids.CopyTo (copy, 0);

			for (int i = 0; i < copy.Length; i++) {
				Assert.That (copy[i].Validity, Is.EqualTo (20160117));
				Assert.That (copy[i].Id, Is.EqualTo (i + 1));
			}

			var list = new List<UniqueId> ();
			foreach (var uid in uids) {
				Assert.That (uid.Validity, Is.EqualTo (20160117));
				list.Add (uid);
			}

			for (int i = 0; i < list.Count; i++)
				Assert.That (list[i].Id, Is.EqualTo (i + 1));
		}

		[Test]
		public void TestDescending ()
		{
			const string example = "20:1";
			var copy = new UniqueId[20];
			UniqueIdRange uids;

			Assert.That (UniqueIdRange.TryParse (example, 20160117, out uids), Is.True, "Failed to parse uids.");
			Assert.That (uids.Validity, Is.EqualTo (20160117), "Validity");
			Assert.That (uids.IsReadOnly, Is.True, "IsReadOnly");
			Assert.That (uids.Start.Id, Is.EqualTo (20), "Start");
			Assert.That (uids.End.Id, Is.EqualTo (1), "End");
			Assert.That (uids.Min.Id, Is.EqualTo (1), "Min");
			Assert.That (uids.Max.Id, Is.EqualTo (20), "Max");
			Assert.That (uids.ToString (), Is.EqualTo (example), "ToString");
			Assert.That (uids, Has.Count.EqualTo (20));

			Assert.That (uids, Does.Not.Contain (new UniqueId (500)));
			Assert.That (uids.IndexOf (new UniqueId (500)), Is.EqualTo (-1));

			for (int i = 0; i < uids.Count; i++) {
				Assert.That (uids.IndexOf (uids[i]), Is.EqualTo (i));
				Assert.That (uids[i].Validity, Is.EqualTo (20160117));
				Assert.That (uids[i].Id, Is.EqualTo (20 - i));
			}

			uids.CopyTo (copy, 0);

			for (int i = 0; i < copy.Length; i++) {
				Assert.That (copy[i].Validity, Is.EqualTo (20160117));
				Assert.That (copy[i].Id, Is.EqualTo (20 - i));
			}

			var list = new List<UniqueId> ();
			foreach (var uid in uids) {
				Assert.That (uid.Validity, Is.EqualTo (20160117));
				list.Add (uid);
			}

			for (int i = 0; i < list.Count; i++)
				Assert.That (list[i].Id, Is.EqualTo (20 - i));
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
			Assert.That (UniqueIdRange.TryParse ("xyz", out _), Is.False);
			Assert.That (UniqueIdRange.TryParse ("1:xyz", out _), Is.False);
			Assert.That (UniqueIdRange.TryParse ("1:*1", out _), Is.False);
			Assert.That (UniqueIdRange.TryParse ("1:1x", out _), Is.False);

			Assert.That (UniqueIdRange.TryParse ("1:*", out var range), Is.True);
			Assert.That (range.Min, Is.EqualTo (UniqueId.MinValue));
			Assert.That (range.Max, Is.EqualTo (UniqueId.MaxValue));
		}
	}
}
