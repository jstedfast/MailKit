//
// AccessControlListTests.cs
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
	public class AccessControlListTests
	{
		[Test]
		public void TestArgumentExceptions ()
		{
			var enumeratedRights = new [] { AccessRight.OpenFolder, AccessRight.CreateFolder };
			var array = new AccessRight[10];

			var rights = new AccessRights (enumeratedRights);
			Assert.Throws<ArgumentNullException> (() => rights.AddRange ((string) null));
			Assert.Throws<ArgumentNullException> (() => rights.AddRange ((IEnumerable<AccessRight>) null));
			Assert.Throws<ArgumentNullException> (() => new AccessRights ((string) null));
			Assert.Throws<ArgumentNullException> (() => new AccessRights ((IEnumerable<AccessRight>) null));
			Assert.Throws<ArgumentOutOfRangeException> (() => { var x = rights [-1]; });
			Assert.Throws<ArgumentNullException> (() => rights.CopyTo (null, 0));
			Assert.Throws<ArgumentOutOfRangeException> (() => rights.CopyTo (array, -1));

			var control = new AccessControl ("control");
			Assert.Throws<ArgumentNullException> (() => new AccessControl (null));
			Assert.Throws<ArgumentNullException> (() => new AccessControl (null, "rk"));
			Assert.Throws<ArgumentNullException> (() => new AccessControl (null, enumeratedRights));
			Assert.Throws<ArgumentNullException> (() => new AccessControl ("name", (string) null));
			Assert.Throws<ArgumentNullException> (() => new AccessControl ("name", (IEnumerable<AccessRight>) null));

			var list = new AccessControlList ();
			Assert.Throws<ArgumentNullException> (() => new AccessControlList (null));
			//Assert.Throws<ArgumentNullException> (() => list.Add (null));
			Assert.Throws<ArgumentNullException> (() => list.AddRange (null));
		}

		[Test]
		public void TestAccessRight ()
		{
			Assert.IsTrue (AccessRight.Administer == new AccessRight (AccessRight.Administer.Right), "==");
			Assert.IsFalse (AccessRight.Administer == new AccessRight (AccessRight.OpenFolder.Right), "==");

			Assert.IsFalse (AccessRight.Administer != new AccessRight (AccessRight.Administer.Right), "!=");
			Assert.IsTrue (AccessRight.Administer != new AccessRight (AccessRight.OpenFolder.Right), "!=");

			Assert.IsTrue (AccessRight.Administer.Equals ((object) new AccessRight (AccessRight.Administer.Right)), "Equals");
			Assert.AreEqual (AccessRight.Administer.GetHashCode (), new AccessRight (AccessRight.Administer.Right).GetHashCode (), "GetHashCode");

			Assert.AreEqual ("a", AccessRight.Administer.ToString (), "ToString");
		}

		[Test]
		public void TestAccessRights ()
		{
			var expected = new [] { AccessRight.OpenFolder, AccessRight.CreateFolder, AccessRight.DeleteFolder, AccessRight.ExpungeFolder, AccessRight.AppendMessages, AccessRight.SetMessageDeleted };
			var rights = new AccessRights ();
			int i;

			Assert.IsFalse (rights.IsReadOnly, "IsReadOnly");

			Assert.IsTrue (rights.Add (AccessRight.OpenFolder), "Add OpenFolder");
			Assert.AreEqual (1, rights.Count, "Count after adding OpenFolder");
			Assert.IsFalse (rights.Add (AccessRight.OpenFolder), "Add OpenFolder again");
			Assert.AreEqual (1, rights.Count, "Count after adding OpenFolder again");

			Assert.IsTrue (rights.Add (AccessRight.CreateFolder.Right), "Add CreateFolder");
			Assert.AreEqual (2, rights.Count, "Count after adding CreateFolder");
			Assert.IsFalse (rights.Add (AccessRight.CreateFolder), "Add CreateFolder again");
			Assert.AreEqual (2, rights.Count, "Count after adding OpenFolder again");

			rights.AddRange (new [] { AccessRight.DeleteFolder, AccessRight.ExpungeFolder });
			Assert.AreEqual (4, rights.Count, "Count after adding DeleteFolder and ExpungeFolder");

			Assert.IsTrue (rights.Contains (AccessRight.DeleteFolder), "Contains DeleteFolder");
			Assert.IsTrue (rights.Contains (AccessRight.ExpungeFolder), "Contains ExpungeFolder");
			Assert.IsFalse (rights.Contains (AccessRight.Administer), "Contains Administer");

			rights.AddRange ("it");
			Assert.AreEqual (6, rights.Count, "Count after adding AppendMessages and SetMessageDeleted");

			Assert.IsTrue (rights.Contains (AccessRight.AppendMessages), "Contains AppendMessages");
			Assert.IsTrue (rights.Contains (AccessRight.SetMessageDeleted), "Contains SetMessageDeleted");
			Assert.IsFalse (rights.Contains (AccessRight.Administer), "Contains Administer");

			for (i = 0; i < 6; i++)
				Assert.AreEqual (expected[i], rights[i], "rights[{0}]", i);

			((ICollection<AccessRight>) rights).Add (AccessRight.Administer);
			Assert.IsTrue (rights.Remove (AccessRight.Administer), "Remove Administer");
			Assert.IsFalse (rights.Remove (AccessRight.Administer), "Remove Administer again");

			i = 0;
			foreach (var right in rights)
				Assert.AreEqual (expected[i], right, "foreach rights[{0}]", i++);

			i = 0;
			foreach (AccessRight right in ((IEnumerable) rights))
				Assert.AreEqual (expected[i], right, "generic foreach rights[{0}]", i++);

			var array = new AccessRight[rights.Count];
			rights.CopyTo (array, 0);

			for (i = 0; i < 6; i++)
				Assert.AreEqual (expected[i], array[i], "CopyTo[{0}]", i);

			Assert.AreEqual ("rkxeit", rights.ToString (), "ToString");
		}

		[Test]
		public void TestAccessControl ()
		{
			var control = new AccessControl ("empty");

			Assert.AreEqual ("empty", control.Name, "Name");
			Assert.AreEqual ("", control.Rights.ToString (), "Rights (empty)");

			control = new AccessControl ("admin", "a");

			Assert.AreEqual ("admin", control.Name, "Name");
			Assert.AreEqual ("a", control.Rights.ToString (), "Rights (admin)");

			control = new AccessControl ("it", new [] { AccessRight.AppendMessages, AccessRight.SetMessageDeleted });

			Assert.AreEqual ("it", control.Name, "Name");
			Assert.AreEqual ("it", control.Rights.ToString (), "Rights (it)");
		}

		[Test]
		public void TestAccessControlList ()
		{
			var list = new AccessControlList (new [] { new AccessControl ("admin", new [] { AccessRight.Administer }) });

			Assert.AreEqual (1, list.Count, "Count");
			Assert.AreEqual ("admin", list[0].Name, "list[0].Name");
		}
	}
}
