//
// UniqueIdSetTests.cs
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
using MailKit.Search;

namespace UnitTests {
	[TestFixture]
	public class UniqueIdSetTests
	{
		[Test]
		public void TestArgumentExceptions ()
		{
			var uids = new UniqueIdSet (SortOrder.Ascending);
			UniqueId uid;

			Assert.That (uids.IsReadOnly, Is.False);

			uids.Add (UniqueId.MinValue);

			Assert.Throws<ArgumentOutOfRangeException> (() => new UniqueIdSet ((SortOrder)500));
			Assert.Throws<ArgumentException> (() => uids.Add (UniqueId.Invalid));
			Assert.Throws<ArgumentNullException> (() => uids.AddRange (null));
			Assert.Throws<ArgumentNullException> (() => uids.CopyTo (null, 0));
			Assert.Throws<ArgumentOutOfRangeException> (() => uids.CopyTo (new UniqueId[1], -1));
			Assert.Throws<ArgumentOutOfRangeException> (() => uids.RemoveAt (-1));
			Assert.Throws<ArgumentOutOfRangeException> (() => uid = uids[-1]);
			Assert.Throws<NotSupportedException> (() => uids[0] = UniqueId.MinValue);
			Assert.Throws<NotSupportedException> (() => uids.Insert (0, UniqueId.MinValue));

			var list = new List<UniqueId> { UniqueId.Invalid };
			Assert.Throws<ArgumentNullException> (() => UniqueIdSet.ToString (null));
			Assert.Throws<ArgumentException> (() => UniqueIdSet.ToString (list));

			Assert.Throws<ArgumentNullException> (() => UniqueIdSet.TryParse (null, out uids));
		}

		[Test]
		public void TestAscendingUniqueIdSet ()
		{
			UniqueId[] uids = {
				new UniqueId (1), new UniqueId (2), new UniqueId (3),
				new UniqueId (4), new UniqueId (5), new UniqueId (6),
				new UniqueId (7), new UniqueId (8), new UniqueId (9)
			};
			var list = new UniqueIdSet (uids, SortOrder.Ascending);
			var actual = list.ToString ();

			Assert.That (actual, Is.EqualTo ("1:9"), "Incorrect initial value.");
			Assert.That (list, Has.Count.EqualTo (9), "Incorrect initial count.");
			Assert.That (list.IndexOf (new UniqueId (500)), Is.EqualTo (-1));
			Assert.That (list, Does.Not.Contain (new UniqueId (500)));
			Assert.That (list.Remove (new UniqueId (500)), Is.False);

			// Test Remove()

			list.Remove (uids[0]);
			actual = list.ToString ();

			Assert.That (actual, Is.EqualTo ("2:9"), "Incorrect results after Remove() #1.");
			Assert.That (list, Has.Count.EqualTo (8), "Incorrect count after Remove() #1.");

			list.Remove (uids[uids.Length - 1]);
			actual = list.ToString ();

			Assert.That (actual, Is.EqualTo ("2:8"), "Incorrect results after Remove() #2.");
			Assert.That (list, Has.Count.EqualTo (7), "Incorrect count after Remove() #2.");

			list.Remove (uids[4]);
			actual = list.ToString ();

			Assert.That (actual, Is.EqualTo ("2:4,6:8"), "Incorrect results after Remove() #3.");
			Assert.That (list, Has.Count.EqualTo (6), "Incorrect count after Remove() #3.");

			// Test Add()

			list.Add (new UniqueId (5));
			actual = list.ToString ();

			Assert.That (actual, Is.EqualTo ("2:8"), "Incorrect results after Add() #1.");
			Assert.That (list, Has.Count.EqualTo (7), "Incorrect count after Add() #1.");

			list.Add (new UniqueId (1));
			actual = list.ToString ();

			Assert.That (actual, Is.EqualTo ("1:8"), "Incorrect results after Add() #2.");
			Assert.That (list, Has.Count.EqualTo (8), "Incorrect count after Add() #2.");

			list.Add (new UniqueId (9));
			actual = list.ToString ();

			Assert.That (actual, Is.EqualTo ("1:9"), "Incorrect results after Add() #3.");
			Assert.That (list, Has.Count.EqualTo (9), "Incorrect count after Add() #3.");

			// Test RemoveAt()

			list.RemoveAt (0);
			actual = list.ToString ();

			Assert.That (actual, Is.EqualTo ("2:9"), "Incorrect results after RemoveAt() #1.");
			Assert.That (list, Has.Count.EqualTo (8), "Incorrect count after RemoveAt() #1.");

			list.RemoveAt (7);
			actual = list.ToString ();

			Assert.That (actual, Is.EqualTo ("2:8"), "Incorrect results after RemoveAt() #2.");
			Assert.That (list, Has.Count.EqualTo (7), "Incorrect count after RemoveAt() #2.");

			list.RemoveAt (3);
			actual = list.ToString ();

			Assert.That (actual, Is.EqualTo ("2:4,6:8"), "Incorrect results after RemoveAt() #3.");
			Assert.That (list, Has.Count.EqualTo (6), "Incorrect count after RemoveAt() #3.");

			// Test adding a range of items

			list.AddRange (uids);
			actual = list.ToString ();

			Assert.That (actual, Is.EqualTo ("1:9"), "Incorrect results after AddRange().");
			Assert.That (list, Has.Count.EqualTo (9), "Incorrect count after AddRange().");

			// Test clearing the list
			list.Clear ();
			Assert.That (list, Is.Empty, "Incorrect count after Clear().");
		}

		[Test]
		public void TestDescendingUniqueIdSet ()
		{
			UniqueId[] uids = {
				new UniqueId (1), new UniqueId (2), new UniqueId (3),
				new UniqueId (4), new UniqueId (5), new UniqueId (6),
				new UniqueId (7), new UniqueId (8), new UniqueId (9)
			};
			var list = new UniqueIdSet (uids, SortOrder.Descending);
			var actual = list.ToString ();

			Assert.That (actual, Is.EqualTo ("9:1"), "Incorrect initial value.");
			Assert.That (list, Has.Count.EqualTo (9), "Incorrect initial count.");

			// Test Remove()

			list.Remove (uids[0]);
			actual = list.ToString ();

			Assert.That (actual, Is.EqualTo ("9:2"), "Incorrect results after Remove() #1.");
			Assert.That (list, Has.Count.EqualTo (8), "Incorrect count after Remove() #1.");

			list.Remove (uids[uids.Length - 1]);
			actual = list.ToString ();

			Assert.That (actual, Is.EqualTo ("8:2"), "Incorrect results after Remove() #2.");
			Assert.That (list, Has.Count.EqualTo (7), "Incorrect count after Remove() #2.");

			list.Remove (uids[4]);
			actual = list.ToString ();

			Assert.That (actual, Is.EqualTo ("8:6,4:2"), "Incorrect results after Remove() #3.");
			Assert.That (list, Has.Count.EqualTo (6), "Incorrect count after Remove() #3.");

			// Test Add()

			list.Add (new UniqueId (5));
			actual = list.ToString ();

			Assert.That (actual, Is.EqualTo ("8:2"), "Incorrect results after Add() #1.");
			Assert.That (list, Has.Count.EqualTo (7), "Incorrect count after Add() #1.");

			list.Add (new UniqueId (1));
			actual = list.ToString ();

			Assert.That (actual, Is.EqualTo ("8:1"), "Incorrect results after Add() #2.");
			Assert.That (list, Has.Count.EqualTo (8), "Incorrect count after Add() #2.");

			list.Add (new UniqueId (9));
			actual = list.ToString ();

			Assert.That (actual, Is.EqualTo ("9:1"), "Incorrect results after Add() #3.");
			Assert.That (list, Has.Count.EqualTo (9), "Incorrect count after Add() #3.");

			// Test RemoveAt()

			list.RemoveAt (0);
			actual = list.ToString ();

			Assert.That (actual, Is.EqualTo ("8:1"), "Incorrect results after RemoveAt() #1.");
			Assert.That (list, Has.Count.EqualTo (8), "Incorrect count after RemoveAt() #1.");

			list.RemoveAt (7);
			actual = list.ToString ();

			Assert.That (actual, Is.EqualTo ("8:2"), "Incorrect results after RemoveAt() #2.");
			Assert.That (list, Has.Count.EqualTo (7), "Incorrect count after RemoveAt() #2.");

			list.RemoveAt (3);
			actual = list.ToString ();

			Assert.That (actual, Is.EqualTo ("8:6,4:2"), "Incorrect results after RemoveAt() #3.");
			Assert.That (list, Has.Count.EqualTo (6), "Incorrect count after RemoveAt() #3.");

			// Test adding a range of items

			list.AddRange (uids);
			actual = list.ToString ();

			Assert.That (actual, Is.EqualTo ("9:1"), "Incorrect results after AddRange().");
			Assert.That (list, Has.Count.EqualTo (9), "Incorrect count after AddRange().");

			// Test clearing the list
			list.Clear ();
			Assert.That (list, Is.Empty, "Incorrect count after Clear().");
		}

		[Test]
		public void TestUnsortedUniqueIdSet ()
		{
			UniqueId[] uids = {
				new UniqueId (1), new UniqueId (2), new UniqueId (3),
				new UniqueId (4), new UniqueId (5), new UniqueId (6),
				new UniqueId (7), new UniqueId (8), new UniqueId (9)
			};
			var list = new UniqueIdSet (uids);
			var actual = list.ToString ();

			Assert.That (actual, Is.EqualTo ("1:9"), "Incorrect initial value.");
			Assert.That (list, Has.Count.EqualTo (9), "Incorrect initial count.");

			// Test Remove()

			list.Remove (uids[0]);
			actual = list.ToString ();

			Assert.That (actual, Is.EqualTo ("2:9"), "Incorrect results after Remove() #1.");
			Assert.That (list, Has.Count.EqualTo (8), "Incorrect count after Remove() #1.");

			list.Remove (uids[uids.Length - 1]);
			actual = list.ToString ();

			Assert.That (actual, Is.EqualTo ("2:8"), "Incorrect results after Remove() #2.");
			Assert.That (list, Has.Count.EqualTo (7), "Incorrect count after Remove() #2.");

			list.Remove (uids[4]);
			actual = list.ToString ();

			Assert.That (actual, Is.EqualTo ("2:4,6:8"), "Incorrect results after Remove() #3.");
			Assert.That (list, Has.Count.EqualTo (6), "Incorrect count after Remove() #3.");

			// Test Add()

			list.Add (new UniqueId (5));
			actual = list.ToString ();

			Assert.That (actual, Is.EqualTo ("2:4,6:8,5"), "Incorrect results after Add() #1.");
			Assert.That (list, Has.Count.EqualTo (7), "Incorrect count after Add() #1.");

			list.Add (new UniqueId (1));
			actual = list.ToString ();

			Assert.That (actual, Is.EqualTo ("2:4,6:8,5,1"), "Incorrect results after Add() #2.");
			Assert.That (list, Has.Count.EqualTo (8), "Incorrect count after Add() #2.");

			list.Add (new UniqueId (9));
			actual = list.ToString ();

			Assert.That (actual, Is.EqualTo ("2:4,6:8,5,1,9"), "Incorrect results after Add() #3.");
			Assert.That (list, Has.Count.EqualTo (9), "Incorrect count after Add() #3.");

			// Test RemoveAt()

			list.RemoveAt (0);
			actual = list.ToString ();

			Assert.That (actual, Is.EqualTo ("3:4,6:8,5,1,9"), "Incorrect results after RemoveAt() #1.");
			Assert.That (list, Has.Count.EqualTo (8), "Incorrect count after RemoveAt() #1.");

			list.RemoveAt (7);
			actual = list.ToString ();

			Assert.That (actual, Is.EqualTo ("3:4,6:8,5,1"), "Incorrect results after RemoveAt() #2.");
			Assert.That (list, Has.Count.EqualTo (7), "Incorrect count after RemoveAt() #2.");

			list.RemoveAt (3);
			actual = list.ToString ();

			Assert.That (actual, Is.EqualTo ("3:4,6,8,5,1"), "Incorrect results after RemoveAt() #3.");
			Assert.That (list, Has.Count.EqualTo (6), "Incorrect count after RemoveAt() #3.");

			// Test adding a range of items

			list.AddRange (uids);
			actual = list.ToString ();

			Assert.That (actual, Is.EqualTo ("3:4,6,8,5,1:2,7,9"), "Incorrect results after AddRange().");
			Assert.That (list, Has.Count.EqualTo (9), "Incorrect count after AddRange().");

			// Test clearing the list
			list.Clear ();
			Assert.That (list, Is.Empty, "Incorrect count after Clear().");
		}

		[Test]
		public void TestNonSequentialUniqueIdSet ()
		{
			UniqueId[] uids = {
				new UniqueId (1), new UniqueId (3), new UniqueId (5),
				new UniqueId (7), new UniqueId (9)
			};
			var list = new UniqueIdSet (uids);
			var actual = list.ToString ();

			Assert.That (actual, Is.EqualTo ("1,3,5,7,9"));

			list = new UniqueIdSet (uids, SortOrder.Ascending);
			actual = list.ToString ();

			Assert.That (actual, Is.EqualTo ("1,3,5,7,9"), "Unexpected result for ascending set.");

			list = new UniqueIdSet (uids, SortOrder.Descending);
			actual = list.ToString ();

			Assert.That (actual, Is.EqualTo ("9,7,5,3,1"), "Unexpected result for descending set.");
		}

		[Test]
		public void TestComplexUniqueIdSet ()
		{
			UniqueId[] uids = {
				new UniqueId (1), new UniqueId (2), new UniqueId (3),
				new UniqueId (5), new UniqueId (6), new UniqueId (9),
				new UniqueId (10), new UniqueId (11), new UniqueId (12),
				new UniqueId (15), new UniqueId (19), new UniqueId (20)
			};
			var list = new UniqueIdSet (uids);
			var actual = list.ToString ();

			Assert.That (actual, Is.EqualTo ("1:3,5:6,9:12,15,19:20"), "Unexpected result for unsorted set.");

			list = new UniqueIdSet (uids, SortOrder.Ascending);
			actual = list.ToString ();

			Assert.That (actual, Is.EqualTo ("1:3,5:6,9:12,15,19:20"), "Unexpected result for ascending set.");

			list = new UniqueIdSet (uids, SortOrder.Descending);
			actual = list.ToString ();

			Assert.That (actual, Is.EqualTo ("20:19,15,12:9,6:5,3:1"), "Unexpected result for descending set.");
		}

		[Test]
		public void TestReversedUniqueIdSet ()
		{
			UniqueId[] uids = {
				new UniqueId (20), new UniqueId (19), new UniqueId (15),
				new UniqueId (12), new UniqueId (11), new UniqueId (10),
				new UniqueId (9), new UniqueId (6), new UniqueId (5),
				new UniqueId (3), new UniqueId (2), new UniqueId (1)
			};
			var list = new UniqueIdSet (uids);
			var actual = list.ToString ();

			Assert.That (actual, Is.EqualTo ("20:19,15,12:9,6:5,3:1"), "Unexpected result for unsorted set.");

			list = new UniqueIdSet (uids, SortOrder.Ascending);
			actual = list.ToString ();

			Assert.That (actual, Is.EqualTo ("1:3,5:6,9:12,15,19:20"), "Unexpected result for ascending set.");
			Assert.That (list.IndexOf (new UniqueId (1)), Is.EqualTo (0), "Unexpected index for descending set.");

			list = new UniqueIdSet (uids, SortOrder.Descending);
			actual = list.ToString ();

			Assert.That (actual, Is.EqualTo ("20:19,15,12:9,6:5,3:1"), "Unexpected result for descending set.");
			Assert.That (list.IndexOf (new UniqueId (1)), Is.EqualTo (11), "Unexpected index for descending set.");
		}

		[Test]
		public void TestMergingRangesAscending ()
		{
			UniqueId[] uids = {
				new UniqueId (1), new UniqueId (2), new UniqueId (3),
				new UniqueId (5), new UniqueId (6), new UniqueId (7),
				new UniqueId (9), new UniqueId (10), new UniqueId (11),
				new UniqueId (8)
			};
			var list = new UniqueIdSet (uids, SortOrder.Ascending);
			var actual = list.ToString ();

			Assert.That (actual, Is.EqualTo ("1:3,5:11"), "Unexpected result for sorted set.");
		}

		[Test]
		public void TestMergingRangesDescending ()
		{
			UniqueId[] uids = {
				new UniqueId (1), new UniqueId (2), new UniqueId (3),
				new UniqueId (5), new UniqueId (6), new UniqueId (7),
				new UniqueId (9), new UniqueId (10), new UniqueId (11),
				new UniqueId (4)
			};
			var list = new UniqueIdSet (uids, SortOrder.Descending);
			var actual = list.ToString ();

			Assert.That (actual, Is.EqualTo ("11:9,7:1"), "Unexpected result for sorted set.");
		}

		[Test]
		public void TestContainsAscending ()
		{
			var uids = new UniqueIdSet (SortOrder.Ascending);

			Assert.That (uids, Does.Not.Contain (new UniqueId (5)), "5");

			uids.Add (new UniqueId (2));
			uids.Add (new UniqueId (3));

			Assert.That (uids, Does.Not.Contain (new UniqueId (1)), "1");
			Assert.That (uids, Does.Contain (new UniqueId (2)), "2");
			Assert.That (uids, Does.Contain (new UniqueId (3)), "3");
			Assert.That (uids, Does.Not.Contain (new UniqueId (4)), "4");
		}

		[Test]
		public void TestContainsDescending ()
		{
			var uids = new UniqueIdSet (SortOrder.Descending);

			Assert.That (uids, Does.Not.Contain (new UniqueId (5)), "5");

			uids.Add (new UniqueId (2));
			uids.Add (new UniqueId (3));

			Assert.That (uids, Does.Not.Contain (new UniqueId (1)), "1");
			Assert.That (uids, Does.Contain (new UniqueId (2)), "2");
			Assert.That (uids, Does.Contain (new UniqueId (3)), "3");
			Assert.That (uids, Does.Not.Contain (new UniqueId (4)), "4");
		}

		[Test]
		public void TestParsingSimple ()
		{
			const string example = "1:3,5:6,9:12,15,19:20";

			Assert.That (UniqueIdSet.TryParse (example, out var uids), Is.True, "Failed to parse uids.");
			Assert.That (uids.SortOrder, Is.EqualTo (SortOrder.Ascending));
			Assert.That (uids.ToString (), Is.EqualTo (example));
		}

		[Test]
		public void TestParsingReversedSet ()
		{
			var ids = new uint[] { 20, 19, 15, 12, 11, 10, 9, 6, 5, 3, 2, 1 };
			const string example = "20:19,15,12:9,6:5,3:1";

			Assert.That (UniqueIdSet.TryParse (example, out var uids), Is.True, "Failed to parse uids.");
			Assert.That (uids.SortOrder, Is.EqualTo (SortOrder.Descending));
			Assert.That (uids.ToString (), Is.EqualTo (example));
			Assert.That (uids, Has.Count.EqualTo (ids.Length));

			for (int i = 0; i < uids.Count; i++)
				Assert.That (uids[i].Id, Is.EqualTo (ids[i]));
		}

		[Test]
		public void TestParsingInvalidInputs ()
		{
			Assert.That (UniqueIdSet.TryParse ("xyz", out _), Is.False);
			Assert.That (UniqueIdSet.TryParse ("1:x", out _), Is.False);
			Assert.That (UniqueIdSet.TryParse ("1:1x", out _), Is.False);
		}

		[Test]
		public void TestEnumerator ()
		{
			var ids = new uint[] { 20, 19, 15, 12, 11, 10, 9, 6, 5, 3, 2, 1 };
			const string example = "20:19,15,12:9,6:5,3:1";
			UniqueIdSet uids;

			Assert.That (UniqueIdSet.TryParse (example, 20160117, out uids), Is.True, "Failed to parse uids.");
			Assert.That (uids.SortOrder, Is.EqualTo (SortOrder.Descending));
			Assert.That (uids.Validity, Is.EqualTo (20160117));
			Assert.That (uids.ToString (), Is.EqualTo (example));
			Assert.That (uids, Has.Count.EqualTo (ids.Length));

			for (int i = 0; i < uids.Count; i++)
				Assert.That (uids[i].Id, Is.EqualTo (ids[i]));

			var list = new List<UniqueId> ();
			foreach (var uid in uids) {
				Assert.That (uid.Validity, Is.EqualTo (20160117));
				list.Add (uid);
			}

			for (int i = 0; i < list.Count; i++)
				Assert.That (list[i].Id, Is.EqualTo (ids[i]));
		}

		[Test]
		public void TestCopyTo ()
		{
			var ids = new uint[] { 20, 19, 15, 12, 11, 10, 9, 6, 5, 3, 2, 1 };
			const string example = "20:19,15,12:9,6:5,3:1";
			var copy = new UniqueId[ids.Length];
			UniqueIdSet uids;

			Assert.That (UniqueIdSet.TryParse (example, 20160117, out uids), Is.True, "Failed to parse uids.");
			Assert.That (uids.SortOrder, Is.EqualTo (SortOrder.Descending));
			Assert.That (uids.Validity, Is.EqualTo (20160117));
			Assert.That (uids.ToString (), Is.EqualTo (example));
			Assert.That (uids, Has.Count.EqualTo (ids.Length));

			for (int i = 0; i < uids.Count; i++) {
				Assert.That (uids[i].Validity, Is.EqualTo (20160117));
				Assert.That (uids[i].Id, Is.EqualTo (ids[i]));
			}

			uids.CopyTo (copy, 0);

			for (int i = 0; i < copy.Length; i++) {
				Assert.That (copy[i].Validity, Is.EqualTo (20160117));
				Assert.That (copy[i].Id, Is.EqualTo (ids[i]));
			}
		}
	}
}
