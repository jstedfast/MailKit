//
// UniqueIdMapTests.cs
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
using System.Collections.Generic;

using NUnit.Framework;

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

			Assert.AreEqual (7, map.Count, "Count");

			for (u = 1; u < 8; u++)
				Assert.IsTrue (map.ContainsKey (new UniqueId (1436832101, u)), "ContainsKey {0}", u);
			Assert.IsFalse (map.ContainsKey (new UniqueId (1436832101, u)), "ContainsKey {0}", u);

			foreach (var key in map.Keys) {
				UniqueId value;

				Assert.AreEqual (map.Source[i], key, "Source[{0}] vs Key[{0}]", i);
				Assert.IsTrue (map.TryGetValue (key, out value), "TryGetValue ({0})", key);
				Assert.AreEqual (map.Destination[i], value, "Destination[{0}] vs value", i);
				i++;
			}

			foreach (var kvp in map)
				Assert.AreEqual (kvp.Value.Id, kvp.Key.Id + 10, "KeyValuePair {0} -> {1}", kvp.Key, kvp.Value);

			foreach (KeyValuePair<UniqueId,UniqueId> kvp in (IEnumerable) map)
				Assert.AreEqual (kvp.Value.Id, kvp.Key.Id + 10, "Generic KeyValuePair {0} -> {1}", kvp.Key, kvp.Value);

			Assert.Throws<ArgumentOutOfRangeException> (() => { var x = map[new UniqueId (27)]; });
			foreach (var uid in map.Source)
				Assert.AreEqual (uid.Id + 10, map[uid].Id, "map[{0}]", uid);
		}
	}
}
