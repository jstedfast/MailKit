//
// AccessControlListTests.cs
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
			Assert.That (AccessRight.Administer == new AccessRight (AccessRight.Administer.Right), Is.True, "==");
			Assert.That (AccessRight.Administer == new AccessRight (AccessRight.OpenFolder.Right), Is.False, "==");

			Assert.That (AccessRight.Administer != new AccessRight (AccessRight.Administer.Right), Is.False, "!=");
			Assert.That (AccessRight.Administer != new AccessRight (AccessRight.OpenFolder.Right), Is.True, "!=");

			Assert.That (AccessRight.Administer.Equals ((object) new AccessRight (AccessRight.Administer.Right)), Is.True, "Equals");
			Assert.That (new AccessRight (AccessRight.Administer.Right).GetHashCode (), Is.EqualTo (AccessRight.Administer.GetHashCode ()), "GetHashCode");

			Assert.That (AccessRight.Administer.ToString (), Is.EqualTo ("a"), "ToString");
		}

		[Test]
		public void TestAccessRights ()
		{
			var expected = new [] { AccessRight.OpenFolder, AccessRight.CreateFolder, AccessRight.DeleteFolder, AccessRight.ExpungeFolder, AccessRight.AppendMessages, AccessRight.SetMessageDeleted };
			var rights = new AccessRights ();
			int i;

			Assert.That (rights.IsReadOnly, Is.False, "IsReadOnly");

			Assert.That (rights.Add (AccessRight.OpenFolder), Is.True, "Add OpenFolder");
			Assert.That (rights, Has.Count.EqualTo (1), "Count after adding OpenFolder");
			Assert.That (rights.Add (AccessRight.OpenFolder), Is.False, "Add OpenFolder again");
			Assert.That (rights, Has.Count.EqualTo (1), "Count after adding OpenFolder again");

			Assert.That (rights.Add (AccessRight.CreateFolder.Right), Is.True, "Add CreateFolder");
			Assert.That (rights, Has.Count.EqualTo (2), "Count after adding CreateFolder");
			Assert.That (rights.Add (AccessRight.CreateFolder), Is.False, "Add CreateFolder again");
			Assert.That (rights, Has.Count.EqualTo (2), "Count after adding OpenFolder again");

			rights.AddRange (new [] { AccessRight.DeleteFolder, AccessRight.ExpungeFolder });
			Assert.That (rights, Has.Count.EqualTo (4), "Count after adding DeleteFolder and ExpungeFolder");

			Assert.That (rights, Does.Contain (AccessRight.DeleteFolder), "Contains DeleteFolder");
			Assert.That (rights, Does.Contain (AccessRight.ExpungeFolder), "Contains ExpungeFolder");
			Assert.That (rights, Does.Not.Contain (AccessRight.Administer), "Contains Administer");

			rights.AddRange ("it");
			Assert.That (rights, Has.Count.EqualTo (6), "Count after adding AppendMessages and SetMessageDeleted");

			Assert.That (rights, Does.Contain (AccessRight.AppendMessages), "Contains AppendMessages");
			Assert.That (rights, Does.Contain (AccessRight.SetMessageDeleted), "Contains SetMessageDeleted");
			Assert.That (rights, Does.Not.Contain (AccessRight.Administer), "Contains Administer");

			for (i = 0; i < 6; i++)
				Assert.That (rights[i], Is.EqualTo (expected[i]), $"rights[{i}]");

			((ICollection<AccessRight>) rights).Add (AccessRight.Administer);
			Assert.That (rights.Remove (AccessRight.Administer), Is.True, "Remove Administer");
			Assert.That (rights.Remove (AccessRight.Administer), Is.False, "Remove Administer again");

			i = 0;
			foreach (var right in rights)
				Assert.That (right, Is.EqualTo (expected[i]), $"foreach rights[{i++}]");

			i = 0;
			foreach (AccessRight right in ((IEnumerable) rights))
				Assert.That (right, Is.EqualTo (expected[i]), $"generic foreach rights[{i++}]");

			var array = new AccessRight[rights.Count];
			rights.CopyTo (array, 0);

			for (i = 0; i < 6; i++)
				Assert.That (array[i], Is.EqualTo (expected[i]), $"CopyTo[{i}]");

			Assert.That (rights.ToString (), Is.EqualTo ("rkxeit"), "ToString");
		}

		[Test]
		public void TestAccessControl ()
		{
			var control = new AccessControl ("empty");

			Assert.That (control.Name, Is.EqualTo ("empty"), "Name");
			Assert.That (control.Rights.ToString (), Is.EqualTo (""), "Rights (empty)");

			control = new AccessControl ("admin", "a");

			Assert.That (control.Name, Is.EqualTo ("admin"), "Name");
			Assert.That (control.Rights.ToString (), Is.EqualTo ("a"), "Rights (admin)");

			control = new AccessControl ("it", new [] { AccessRight.AppendMessages, AccessRight.SetMessageDeleted });

			Assert.That (control.Name, Is.EqualTo ("it"), "Name");
			Assert.That (control.Rights.ToString (), Is.EqualTo ("it"), "Rights (it)");
		}

		[Test]
		public void TestAccessControlList ()
		{
			var list = new AccessControlList (new [] { new AccessControl ("admin", new [] { AccessRight.Administer }) });

			Assert.That (list, Has.Count.EqualTo (1), "Count");
			Assert.That (list[0].Name, Is.EqualTo ("admin"), "list[0].Name");
		}
	}
}
