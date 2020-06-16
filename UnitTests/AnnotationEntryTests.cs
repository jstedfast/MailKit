//
// AnnotationEntryTests.cs
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

using NUnit.Framework;

using MailKit;

namespace UnitTests {
	[TestFixture]
	public class AnnotationEntryTests
	{
		[Test]
		public void TestArgumentExceptions ()
		{
			Assert.Throws<ArgumentNullException> (() => new AnnotationEntry (null));
			Assert.Throws<ArgumentException> (() => new AnnotationEntry (string.Empty));
			Assert.Throws<ArgumentException> (() => new AnnotationEntry ("x")); // paths must begin with '/'
			Assert.Throws<ArgumentException> (() => new AnnotationEntry ("/1.2.3.4.5")); // paths must not begin with a part-spec
			Assert.Throws<ArgumentException> (() => new AnnotationEntry ("/台北/日本語")); // paths may not contain non-ascii characters
			Assert.Throws<ArgumentException> (() => new AnnotationEntry ("/path/0wnz")); // path components must not begin with a number
			Assert.Throws<ArgumentException> (() => new AnnotationEntry ("/root//node")); // path components must not contain "//"
			Assert.Throws<ArgumentException> (() => new AnnotationEntry ("/root/")); // path components must not end with '/'
			Assert.Throws<ArgumentException> (() => new AnnotationEntry ("/root..node")); // path components must not contain ".."
			Assert.Throws<ArgumentException> (() => new AnnotationEntry ("/root./node")); // path components must not end with '.'
			Assert.Throws<ArgumentException> (() => new AnnotationEntry ("/root.")); // path components must not end with '.'

			Assert.Throws<ArgumentNullException> (() => new AnnotationEntry ((string) null, "/comment"));
			Assert.Throws<ArgumentNullException> (() => new AnnotationEntry ("1", null));
			Assert.Throws<ArgumentException> (() => new AnnotationEntry ("abc", "/comment")); // invalid part-spec
			Assert.Throws<ArgumentException> (() => new AnnotationEntry ("1.", "/comment")); // invalid part-spec
			Assert.Throws<ArgumentException> (() => new AnnotationEntry ("1..", "/comment")); // invalid part-spec
			Assert.Throws<ArgumentException> (() => new AnnotationEntry ("1..2", "/comment")); // invalid part-spec

			Assert.Throws<ArgumentNullException> (() => new AnnotationEntry ((BodyPart) null, "/comment"));

			Assert.Throws<ArgumentNullException> (() => AnnotationEntry.Parse (null));
		}

		[Test]
		public void TestBasicFunctionality ()
		{
			var body = new BodyPartBasic {
				PartSpecifier = "1.2.3.4"
			};
			AnnotationEntry entry;

			entry = new AnnotationEntry ("/comment");
			Assert.AreEqual ("/comment", entry.Entry, "Entry");
			Assert.IsNull (entry.PartSpecifier, "PartSpecifier");
			Assert.AreEqual ("/comment", entry.Path, "Path");
			Assert.AreEqual (AnnotationScope.Both, entry.Scope, "Scope");

			entry = new AnnotationEntry ("/comment", AnnotationScope.Private);
			Assert.AreEqual ("/comment.priv", entry.Entry, "Entry");
			Assert.IsNull (entry.PartSpecifier, "PartSpecifier");
			Assert.AreEqual ("/comment", entry.Path, "Path");
			Assert.AreEqual (AnnotationScope.Private, entry.Scope, "Scope");

			entry = new AnnotationEntry ("/comment", AnnotationScope.Shared);
			Assert.AreEqual ("/comment.shared", entry.Entry, "Entry");
			Assert.IsNull (entry.PartSpecifier, "PartSpecifier");
			Assert.AreEqual ("/comment", entry.Path, "Path");
			Assert.AreEqual (AnnotationScope.Shared, entry.Scope, "Scope");


			entry = new AnnotationEntry ("1.2.3.4", "/comment");
			Assert.AreEqual ("/1.2.3.4/comment", entry.Entry, "Entry");
			Assert.AreEqual ("1.2.3.4", entry.PartSpecifier, "PartSpecifier");
			Assert.AreEqual ("/comment", entry.Path, "Path");
			Assert.AreEqual (AnnotationScope.Both, entry.Scope, "Scope");

			entry = new AnnotationEntry ("1.2.3.4", "/comment", AnnotationScope.Private);
			Assert.AreEqual ("/1.2.3.4/comment.priv", entry.Entry, "Entry");
			Assert.AreEqual ("1.2.3.4", entry.PartSpecifier, "PartSpecifier");
			Assert.AreEqual ("/comment", entry.Path, "Path");
			Assert.AreEqual (AnnotationScope.Private, entry.Scope, "Scope");

			entry = new AnnotationEntry ("1.2.3.4", "/comment", AnnotationScope.Shared);
			Assert.AreEqual ("/1.2.3.4/comment.shared", entry.Entry, "Entry");
			Assert.AreEqual ("1.2.3.4", entry.PartSpecifier, "PartSpecifier");
			Assert.AreEqual ("/comment", entry.Path, "Path");
			Assert.AreEqual (AnnotationScope.Shared, entry.Scope, "Scope");


			entry = new AnnotationEntry (body, "/comment");
			Assert.AreEqual ("/1.2.3.4/comment", entry.Entry, "Entry");
			Assert.AreEqual ("1.2.3.4", entry.PartSpecifier, "PartSpecifier");
			Assert.AreEqual ("/comment", entry.Path, "Path");
			Assert.AreEqual (AnnotationScope.Both, entry.Scope, "Scope");

			entry = new AnnotationEntry (body, "/comment", AnnotationScope.Private);
			Assert.AreEqual ("/1.2.3.4/comment.priv", entry.Entry, "Entry");
			Assert.AreEqual ("1.2.3.4", entry.PartSpecifier, "PartSpecifier");
			Assert.AreEqual ("/comment", entry.Path, "Path");
			Assert.AreEqual (AnnotationScope.Private, entry.Scope, "Scope");

			entry = new AnnotationEntry (body, "/comment", AnnotationScope.Shared);
			Assert.AreEqual ("/1.2.3.4/comment.shared", entry.Entry, "Entry");
			Assert.AreEqual ("1.2.3.4", entry.PartSpecifier, "PartSpecifier");
			Assert.AreEqual ("/comment", entry.Path, "Path");
			Assert.AreEqual (AnnotationScope.Shared, entry.Scope, "Scope");
		}

		[Test]
		public void TestEquality ()
		{
			var comment = new AnnotationEntry ("/comment");

			Assert.AreEqual (AnnotationEntry.Comment, comment, "AreEqual");
			Assert.IsTrue (AnnotationEntry.Comment.Equals (comment), ".Equals");
			Assert.IsTrue (comment == AnnotationEntry.Comment, "/comment == /comment");
			Assert.IsTrue (AnnotationEntry.PrivateComment != AnnotationEntry.SharedComment, "/comment.priv != /comment.shared");

			Assert.IsFalse (AnnotationEntry.Comment.Equals ((object) null), "/comment.Equals ((object) null)");
			Assert.IsFalse (AnnotationEntry.Comment.Equals ((AnnotationEntry) null), "/comment.Equals ((AnnotationEntry) null)");
			Assert.IsFalse (AnnotationEntry.Comment == null, "/comment == null");
			Assert.IsTrue (AnnotationEntry.Comment != null, "/comment != null");
		}

		[Test]
		public void TestParse ()
		{
			AnnotationEntry entry;

			Assert.Throws<FormatException> (() => AnnotationEntry.Parse (string.Empty), "string.Empty");

			// invalid part-specs
			Assert.Throws<FormatException> (() => AnnotationEntry.Parse ("/1./comment"), "/1./comment");
			Assert.Throws<FormatException> (() => AnnotationEntry.Parse ("/1../comment"), "/1../comment");
			Assert.Throws<FormatException> (() => AnnotationEntry.Parse ("/1..2/comment"), "/1..2/comment");

			// invalid paths
			Assert.Throws<FormatException> (() => AnnotationEntry.Parse ("x"), "x"); // paths must begin with '/'
			Assert.Throws<FormatException> (() => AnnotationEntry.Parse ("/1a/comment"), "/1a/comment"); // invalid character in part-spec
			Assert.Throws<FormatException> (() => AnnotationEntry.Parse ("/1.2.3.4.5"), "/1.2.3.4.5"); // paths must not contain only a part-spec
			Assert.Throws<FormatException> (() => AnnotationEntry.Parse ("/台北/日本語"), "/台北/日本語"); // paths may not contain non-ascii characters
			Assert.Throws<FormatException> (() => AnnotationEntry.Parse ("/path/0wnz"), "/path/0wnz"); // path components must not begin with a number
			Assert.Throws<FormatException> (() => AnnotationEntry.Parse ("/root//node"), "/root//node"); // path components must not contain "//"
			Assert.Throws<FormatException> (() => AnnotationEntry.Parse ("/root/"), "/root/"); // path components must not end with '/'
			Assert.Throws<FormatException> (() => AnnotationEntry.Parse ("/root..node"), "/root..node"); // path components must not contain ".."
			Assert.Throws<FormatException> (() => AnnotationEntry.Parse ("/root./node"), "/root./node"); // path components must not end with '.'
			Assert.Throws<FormatException> (() => AnnotationEntry.Parse ("/root."), "/root."); // path components must not end with '.'

			try {
				entry = AnnotationEntry.Parse ("/comment");
			} catch (Exception ex) {
				Assert.Fail ("Did not expect: {0}", ex);
				return;
			}
			Assert.AreEqual ("/comment", entry.Entry, "Entry");
			Assert.IsNull (entry.PartSpecifier, "PartSpecifier");
			Assert.AreEqual ("/comment", entry.Path, "Path");
			Assert.AreEqual (AnnotationScope.Both, entry.Scope, "Scope");

			try {
				entry = AnnotationEntry.Parse ("/comment.priv");
			} catch (Exception ex) {
				Assert.Fail ("Did not expect: {0}", ex);
				return;
			}
			Assert.AreEqual ("/comment.priv", entry.Entry, "Entry");
			Assert.IsNull (entry.PartSpecifier, "PartSpecifier");
			Assert.AreEqual ("/comment", entry.Path, "Path");
			Assert.AreEqual (AnnotationScope.Private, entry.Scope, "Scope");

			try {
				entry = AnnotationEntry.Parse ("/comment.shared");
			} catch (Exception ex) {
				Assert.Fail ("Did not expect: {0}", ex);
				return;
			}
			entry = new AnnotationEntry ("/comment", AnnotationScope.Shared);
			Assert.AreEqual ("/comment.shared", entry.Entry, "Entry");
			Assert.IsNull (entry.PartSpecifier, "PartSpecifier");
			Assert.AreEqual ("/comment", entry.Path, "Path");
			Assert.AreEqual (AnnotationScope.Shared, entry.Scope, "Scope");

			try {
				entry = AnnotationEntry.Parse ("/1.2.3.4/comment");
			} catch (Exception ex) {
				Assert.Fail ("Did not expect: {0}", ex);
				return;
			}
			Assert.AreEqual ("/1.2.3.4/comment", entry.Entry, "Entry");
			Assert.AreEqual ("1.2.3.4", entry.PartSpecifier, "PartSpecifier");
			Assert.AreEqual ("/comment", entry.Path, "Path");
			Assert.AreEqual (AnnotationScope.Both, entry.Scope, "Scope");

			try {
				entry = AnnotationEntry.Parse ("/1.2.3.4/comment.priv");
			} catch (Exception ex) {
				Assert.Fail ("Did not expect: {0}", ex);
				return;
			}
			Assert.AreEqual ("/1.2.3.4/comment.priv", entry.Entry, "Entry");
			Assert.AreEqual ("1.2.3.4", entry.PartSpecifier, "PartSpecifier");
			Assert.AreEqual ("/comment", entry.Path, "Path");
			Assert.AreEqual (AnnotationScope.Private, entry.Scope, "Scope");

			try {
				entry = AnnotationEntry.Parse ("/1.2.3.4/comment.shared");
			} catch (Exception ex) {
				Assert.Fail ("Did not expect: {0}", ex);
				return;
			}
			Assert.AreEqual ("/1.2.3.4/comment.shared", entry.Entry, "Entry");
			Assert.AreEqual ("1.2.3.4", entry.PartSpecifier, "PartSpecifier");
			Assert.AreEqual ("/comment", entry.Path, "Path");
			Assert.AreEqual (AnnotationScope.Shared, entry.Scope, "Scope");
		}

		[Test]
		public void TestCreate ()
		{
			AnnotationEntry entry;

			try {
				entry = AnnotationEntry.Create ("/comment");
			} catch (Exception ex) {
				Assert.Fail ("Did not expect: {0}", ex);
				return;
			}
			Assert.AreEqual (AnnotationEntry.Comment, entry, "/comment");

			try {
				entry = AnnotationEntry.Create ("/comment.priv");
			} catch (Exception ex) {
				Assert.Fail ("Did not expect: {0}", ex);
				return;
			}
			Assert.AreEqual (AnnotationEntry.PrivateComment, entry, "/comment.priv");

			try {
				entry = AnnotationEntry.Create ("/comment.shared");
			} catch (Exception ex) {
				Assert.Fail ("Did not expect: {0}", ex);
				return;
			}
			Assert.AreEqual (AnnotationEntry.SharedComment, entry, "/comment.shared");

			try {
				entry = AnnotationEntry.Create ("/flags");
			} catch (Exception ex) {
				Assert.Fail ("Did not expect: {0}", ex);
				return;
			}
			Assert.AreEqual (AnnotationEntry.Flags, entry, "/flags");

			try {
				entry = AnnotationEntry.Create ("/flags.priv");
			} catch (Exception ex) {
				Assert.Fail ("Did not expect: {0}", ex);
				return;
			}
			Assert.AreEqual (AnnotationEntry.PrivateFlags, entry, "/flags.priv");

			try {
				entry = AnnotationEntry.Create ("/flags.shared");
			} catch (Exception ex) {
				Assert.Fail ("Did not expect: {0}", ex);
				return;
			}
			Assert.AreEqual (AnnotationEntry.SharedFlags, entry, "/flags.shared");

			try {
				entry = AnnotationEntry.Create ("/altsubject");
			} catch (Exception ex) {
				Assert.Fail ("Did not expect: {0}", ex);
				return;
			}
			Assert.AreEqual (AnnotationEntry.AltSubject, entry, "/altsubject");

			try {
				entry = AnnotationEntry.Create ("/altsubject.priv");
			} catch (Exception ex) {
				Assert.Fail ("Did not expect: {0}", ex);
				return;
			}
			Assert.AreEqual (AnnotationEntry.PrivateAltSubject, entry, "/altsubject.priv");

			try {
				entry = AnnotationEntry.Create ("/altsubject.shared");
			} catch (Exception ex) {
				Assert.Fail ("Did not expect: {0}", ex);
				return;
			}
			Assert.AreEqual (AnnotationEntry.SharedAltSubject, entry, "/altsubject.shared");

			try {
				entry = AnnotationEntry.Create ("/1.2.3.4/comment");
			} catch (Exception ex) {
				Assert.Fail ("Did not expect: {0}", ex);
				return;
			}
			Assert.AreEqual ("/1.2.3.4/comment", entry.Entry, "Entry");
			Assert.AreEqual ("1.2.3.4", entry.PartSpecifier, "PartSpecifier");
			Assert.AreEqual ("/comment", entry.Path, "Path");
			Assert.AreEqual (AnnotationScope.Both, entry.Scope, "Scope");
		}
	}
}
