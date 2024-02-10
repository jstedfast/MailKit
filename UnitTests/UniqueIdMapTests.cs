//
// UniqueIdMapTests.cs
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

using System.Collections;

using MailKit;

namespace UnitTests {
	[TestFixture]
	public class UniqueIdMapTests
	{
		[Test]
		public void TestArgumentExceptions ()
		{
			Assert.Throws<ArgumentNullException> (() => new UniqueIdMap (null, new [] { UniqueId.MinValue }));
			Assert.Throws<ArgumentNullException> (() => new UniqueIdMap (new [] { UniqueId.MinValue }, null));
		}

		[Test]
		public void TestBasicFunctionality ()
		{
			var map = new UniqueIdMap (new UniqueIdRange (1436832101, 1, 7), new UniqueIdRange (1436832128, 11, 17));
			int i = 0;
			uint u;

			Assert.That (map, Has.Count.EqualTo (7), "Count");

			for (u = 1; u < 8; u++)
				Assert.That (map.ContainsKey (new UniqueId (1436832101, u)), Is.True, $"ContainsKey {u}");
			Assert.That (map.ContainsKey (new UniqueId (1436832101, u)), Is.False, $"ContainsKey {u}");

			foreach (var key in map.Keys) {
				Assert.That (key, Is.EqualTo (map.Source[i]), $"Source[{i}] vs Key[{i}]");
				Assert.That (map.TryGetValue (key, out var value), Is.True, $"TryGetValue ({key})");
				Assert.That (value, Is.EqualTo (map.Destination[i]), $"Destination[{i}] vs value");
				i++;
			}

			foreach (var kvp in map)
				Assert.That (kvp.Key.Id + 10, Is.EqualTo (kvp.Value.Id), $"KeyValuePair {kvp.Key} -> {kvp.Value}");

			foreach (KeyValuePair<UniqueId,UniqueId> kvp in (IEnumerable) map)
				Assert.That (kvp.Key.Id + 10, Is.EqualTo (kvp.Value.Id), $"Generic KeyValuePair {kvp.Key} -> {kvp.Value}");

			Assert.Throws<ArgumentOutOfRangeException> (() => { var x = map[new UniqueId (27)]; });
			foreach (var uid in map.Source)
				Assert.That (map[uid].Id, Is.EqualTo (uid.Id + 10), $"map[{uid}]");
		}
	}
}
