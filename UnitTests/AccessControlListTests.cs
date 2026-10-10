//
// AccessControlListTests.cs
//
// Author: Jeffrey Stedfast <jestedfa@microsoft.com>
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

			var list = new AccessControlList (new [] { control });
			var controls = new AccessControl[1];
			Assert.Throws<ArgumentNullException> (() => new AccessControlList (null));
			Assert.Throws<ArgumentNullException> (() => new AccessControlList (new AccessControl[] { control, null }));
			Assert.Throws<ArgumentNullException> (() => list.Add (null));
			Assert.Throws<ArgumentNullException> (() => list.AddRange (null));
			Assert.Throws<ArgumentNullException> (() => list.AddRange (new AccessControl[] { new AccessControl ("valid"), null }));
			Assert.That (list, Has.Count.EqualTo (1), "AddRange should not add anything if any of the controls are null");
			Assert.Throws<ArgumentOutOfRangeException> (() => list.Insert (-1, control));
			Assert.Throws<ArgumentOutOfRangeException> (() => list.Insert (2, control));
			Assert.Throws<ArgumentNullException> (() => list.Insert (0, null));
			Assert.Throws<ArgumentNullException> (() => list.Contains (null));
			Assert.Throws<ArgumentNullException> (() => list.IndexOf (null));
			Assert.Throws<ArgumentNullException> (() => list.Remove (null));
			Assert.Throws<ArgumentOutOfRangeException> (() => list.RemoveAt (-1));
			Assert.Throws<ArgumentOutOfRangeException> (() => list.RemoveAt (1));
			Assert.Throws<ArgumentOutOfRangeException> (() => { var x = list[-1]; });
			Assert.Throws<ArgumentOutOfRangeException> (() => { var x = list[1]; });
			Assert.Throws<ArgumentOutOfRangeException> (() => list[-1] = control);
			Assert.Throws<ArgumentOutOfRangeException> (() => list[1] = control);
			Assert.Throws<ArgumentNullException> (() => list[0] = null);
			Assert.Throws<ArgumentNullException> (() => list.CopyTo (null, 0));
			Assert.Throws<ArgumentOutOfRangeException> (() => list.CopyTo (controls, -1));
			Assert.Throws<ArgumentOutOfRangeException> (() => list.CopyTo (controls, 1));
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
		public void TestAccessControlListConstructors ()
		{
			var admin = new AccessControl ("admin", new [] { AccessRight.Administer });
			var anyone = new AccessControl ("anyone", "lr");

			var list = new AccessControlList ();
			Assert.That (list, Is.Empty, "Default constructor");
			Assert.That (list.IsReadOnly, Is.False, "IsReadOnly");

			list = new AccessControlList (new [] { admin, anyone });
			Assert.That (list, Has.Count.EqualTo (2), "Count");
			Assert.That (list[0], Is.SameAs (admin), "list[0]");
			Assert.That (list[1], Is.SameAs (anyone), "list[1]");
			Assert.That (list[0].Name, Is.EqualTo ("admin"), "list[0].Name");
			Assert.That (list[1].Rights.ToString (), Is.EqualTo ("lr"), "list[1].Rights");
		}

		[Test]
		public void TestAccessControlList ()
		{
			var admin = new AccessControl ("admin", "a");
			var anyone = new AccessControl ("anyone", "lr");
			var owner = new AccessControl ("owner", "lrswipkxtea");
			var guest = new AccessControl ("guest", "l");
			var list = new AccessControlList ();

			list.Add (admin);
			Assert.That (list, Has.Count.EqualTo (1), "Count after Add");
			Assert.That (list[0], Is.SameAs (admin), "list[0] after Add");

			list.AddRange (new [] { anyone, owner });
			Assert.That (list, Has.Count.EqualTo (3), "Count after AddRange");
			Assert.That (list[1], Is.SameAs (anyone), "list[1] after AddRange");
			Assert.That (list[2], Is.SameAs (owner), "list[2] after AddRange");

			list.Insert (0, guest);
			Assert.That (list, Has.Count.EqualTo (4), "Count after Insert");
			Assert.That (list[0], Is.SameAs (guest), "list[0] after Insert");
			Assert.That (list[1], Is.SameAs (admin), "list[1] after Insert");

			list.Insert (list.Count, guest);
			Assert.That (list, Has.Count.EqualTo (5), "Count after Insert at end");
			Assert.That (list[4], Is.SameAs (guest), "list[4] after Insert at end");
			list.RemoveAt (4);
			Assert.That (list, Has.Count.EqualTo (4), "Count after RemoveAt");

			Assert.That (list.Contains (anyone), Is.True, "Contains anyone");
			Assert.That (list.Contains (new AccessControl ("anyone", "lr")), Is.False, "Contains uses reference equality");
			Assert.That (list.IndexOf (guest), Is.EqualTo (0), "IndexOf guest");
			Assert.That (list.IndexOf (owner), Is.EqualTo (3), "IndexOf owner");
			Assert.That (list.IndexOf (new AccessControl ("missing")), Is.EqualTo (-1), "IndexOf missing");

			var replacement = new AccessControl ("anyone", "l");
			list[2] = replacement;
			Assert.That (list[2], Is.SameAs (replacement), "list[2] after set");
			Assert.That (list.Contains (anyone), Is.False, "Contains anyone after set");

			var expected = new [] { guest, admin, replacement, owner };
			int i = 0;

			foreach (var control in list)
				Assert.That (control, Is.SameAs (expected[i]), $"foreach list[{i++}]");

			i = 0;
			foreach (AccessControl control in (IEnumerable) list)
				Assert.That (control, Is.SameAs (expected[i]), $"non-generic foreach list[{i++}]");

			var array = new AccessControl[list.Count + 1];
			list.CopyTo (array, 1);
			Assert.That (array[0], Is.Null, "CopyTo[0]");
			for (i = 0; i < expected.Length; i++)
				Assert.That (array[i + 1], Is.SameAs (expected[i]), $"CopyTo[{i + 1}]");

			IReadOnlyList<AccessControl> readOnly = list;
			Assert.That (readOnly, Has.Count.EqualTo (4), "IReadOnlyList.Count");
			Assert.That (readOnly[3], Is.SameAs (owner), "IReadOnlyList indexer");

			Assert.That (list.Remove (guest), Is.True, "Remove guest");
			Assert.That (list.Remove (guest), Is.False, "Remove guest again");
			Assert.That (list, Has.Count.EqualTo (3), "Count after Remove");
			Assert.That (list[0], Is.SameAs (admin), "list[0] after Remove");

			list.RemoveAt (1);
			Assert.That (list, Has.Count.EqualTo (2), "Count after RemoveAt");
			Assert.That (list[1], Is.SameAs (owner), "list[1] after RemoveAt");

			list.Clear ();
			Assert.That (list, Is.Empty, "Count after Clear");
		}
	}
}
