//
// AnnotationEntryTests.cs
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
			Assert.That (entry.Entry, Is.EqualTo ("/comment"), "Entry");
			Assert.That (entry.PartSpecifier, Is.Null, "PartSpecifier");
			Assert.That (entry.Path, Is.EqualTo ("/comment"), "Path");
			Assert.That (entry.Scope, Is.EqualTo (AnnotationScope.Both), "Scope");

			entry = new AnnotationEntry ("/comment", AnnotationScope.Private);
			Assert.That (entry.Entry, Is.EqualTo ("/comment.priv"), "Entry");
			Assert.That (entry.PartSpecifier, Is.Null, "PartSpecifier");
			Assert.That (entry.Path, Is.EqualTo ("/comment"), "Path");
			Assert.That (entry.Scope, Is.EqualTo (AnnotationScope.Private), "Scope");

			entry = new AnnotationEntry ("/comment", AnnotationScope.Shared);
			Assert.That (entry.Entry, Is.EqualTo ("/comment.shared"), "Entry");
			Assert.That (entry.PartSpecifier, Is.Null, "PartSpecifier");
			Assert.That (entry.Path, Is.EqualTo ("/comment"), "Path");
			Assert.That (entry.Scope, Is.EqualTo (AnnotationScope.Shared), "Scope");


			entry = new AnnotationEntry ("1.2.3.4", "/comment");
			Assert.That (entry.Entry, Is.EqualTo ("/1.2.3.4/comment"), "Entry");
			Assert.That (entry.PartSpecifier, Is.EqualTo ("1.2.3.4"), "PartSpecifier");
			Assert.That (entry.Path, Is.EqualTo ("/comment"), "Path");
			Assert.That (entry.Scope, Is.EqualTo (AnnotationScope.Both), "Scope");

			entry = new AnnotationEntry ("1.2.3.4", "/comment", AnnotationScope.Private);
			Assert.That (entry.Entry, Is.EqualTo ("/1.2.3.4/comment.priv"), "Entry");
			Assert.That (entry.PartSpecifier, Is.EqualTo ("1.2.3.4"), "PartSpecifier");
			Assert.That (entry.Path, Is.EqualTo ("/comment"), "Path");
			Assert.That (entry.Scope, Is.EqualTo (AnnotationScope.Private), "Scope");

			entry = new AnnotationEntry ("1.2.3.4", "/comment", AnnotationScope.Shared);
			Assert.That (entry.Entry, Is.EqualTo ("/1.2.3.4/comment.shared"), "Entry");
			Assert.That (entry.PartSpecifier, Is.EqualTo ("1.2.3.4"), "PartSpecifier");
			Assert.That (entry.Path, Is.EqualTo ("/comment"), "Path");
			Assert.That (entry.Scope, Is.EqualTo (AnnotationScope.Shared), "Scope");


			entry = new AnnotationEntry (body, "/comment");
			Assert.That (entry.Entry, Is.EqualTo ("/1.2.3.4/comment"), "Entry");
			Assert.That (entry.PartSpecifier, Is.EqualTo ("1.2.3.4"), "PartSpecifier");
			Assert.That (entry.Path, Is.EqualTo ("/comment"), "Path");
			Assert.That (entry.Scope, Is.EqualTo (AnnotationScope.Both), "Scope");

			entry = new AnnotationEntry (body, "/comment", AnnotationScope.Private);
			Assert.That (entry.Entry, Is.EqualTo ("/1.2.3.4/comment.priv"), "Entry");
			Assert.That (entry.PartSpecifier, Is.EqualTo ("1.2.3.4"), "PartSpecifier");
			Assert.That (entry.Path, Is.EqualTo ("/comment"), "Path");
			Assert.That (entry.Scope, Is.EqualTo (AnnotationScope.Private), "Scope");

			entry = new AnnotationEntry (body, "/comment", AnnotationScope.Shared);
			Assert.That (entry.Entry, Is.EqualTo ("/1.2.3.4/comment.shared"), "Entry");
			Assert.That (entry.PartSpecifier, Is.EqualTo ("1.2.3.4"), "PartSpecifier");
			Assert.That (entry.Path, Is.EqualTo ("/comment"), "Path");
			Assert.That (entry.Scope, Is.EqualTo (AnnotationScope.Shared), "Scope");
		}

		[Test]
		public void TestEquality ()
		{
			var comment = new AnnotationEntry ("/comment");

			Assert.That (comment, Is.EqualTo (AnnotationEntry.Comment), "AreEqual");
			Assert.That (AnnotationEntry.Comment.Equals (comment), Is.True, ".Equals");
			Assert.That (comment == AnnotationEntry.Comment, Is.True, "/comment == /comment");
			Assert.That (AnnotationEntry.PrivateComment != AnnotationEntry.SharedComment, Is.True, "/comment.priv != /comment.shared");

			Assert.That (AnnotationEntry.Comment.Equals ((object) null), Is.False, "/comment.Equals ((object) null)");
			Assert.That (AnnotationEntry.Comment.Equals ((AnnotationEntry) null), Is.False, "/comment.Equals ((AnnotationEntry) null)");
			Assert.That (AnnotationEntry.Comment == null, Is.False, "/comment == null");
			Assert.That (AnnotationEntry.Comment != null, Is.True, "/comment != null");
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
				Assert.Fail ($"Did not expect: {ex}");
				return;
			}
			Assert.That (entry.Entry, Is.EqualTo ("/comment"), "Entry");
			Assert.That (entry.PartSpecifier, Is.Null, "PartSpecifier");
			Assert.That (entry.Path, Is.EqualTo ("/comment"), "Path");
			Assert.That (entry.Scope, Is.EqualTo (AnnotationScope.Both), "Scope");

			try {
				entry = AnnotationEntry.Parse ("/comment.priv");
			} catch (Exception ex) {
				Assert.Fail ($"Did not expect: {ex}");
				return;
			}
			Assert.That (entry.Entry, Is.EqualTo ("/comment.priv"), "Entry");
			Assert.That (entry.PartSpecifier, Is.Null, "PartSpecifier");
			Assert.That (entry.Path, Is.EqualTo ("/comment"), "Path");
			Assert.That (entry.Scope, Is.EqualTo (AnnotationScope.Private), "Scope");

			try {
				entry = AnnotationEntry.Parse ("/comment.shared");
			} catch (Exception ex) {
				Assert.Fail ($"Did not expect: {ex}");
				return;
			}
			entry = new AnnotationEntry ("/comment", AnnotationScope.Shared);
			Assert.That (entry.Entry, Is.EqualTo ("/comment.shared"), "Entry");
			Assert.That (entry.PartSpecifier, Is.Null, "PartSpecifier");
			Assert.That (entry.Path, Is.EqualTo ("/comment"), "Path");
			Assert.That (entry.Scope, Is.EqualTo (AnnotationScope.Shared), "Scope");

			try {
				entry = AnnotationEntry.Parse ("/1.2.3.4/comment");
			} catch (Exception ex) {
				Assert.Fail ($"Did not expect: {ex}");
				return;
			}
			Assert.That (entry.Entry, Is.EqualTo ("/1.2.3.4/comment"), "Entry");
			Assert.That (entry.PartSpecifier, Is.EqualTo ("1.2.3.4"), "PartSpecifier");
			Assert.That (entry.Path, Is.EqualTo ("/comment"), "Path");
			Assert.That (entry.Scope, Is.EqualTo (AnnotationScope.Both), "Scope");

			try {
				entry = AnnotationEntry.Parse ("/1.2.3.4/comment.priv");
			} catch (Exception ex) {
				Assert.Fail ($"Did not expect: {ex}");
				return;
			}
			Assert.That (entry.Entry, Is.EqualTo ("/1.2.3.4/comment.priv"), "Entry");
			Assert.That (entry.PartSpecifier, Is.EqualTo ("1.2.3.4"), "PartSpecifier");
			Assert.That (entry.Path, Is.EqualTo ("/comment"), "Path");
			Assert.That (entry.Scope, Is.EqualTo (AnnotationScope.Private), "Scope");

			try {
				entry = AnnotationEntry.Parse ("/1.2.3.4/comment.shared");
			} catch (Exception ex) {
				Assert.Fail ($"Did not expect: {ex}");
				return;
			}
			Assert.That (entry.Entry, Is.EqualTo ("/1.2.3.4/comment.shared"), "Entry");
			Assert.That (entry.PartSpecifier, Is.EqualTo ("1.2.3.4"), "PartSpecifier");
			Assert.That (entry.Path, Is.EqualTo ("/comment"), "Path");
			Assert.That (entry.Scope, Is.EqualTo (AnnotationScope.Shared), "Scope");
		}

		[Test]
		public void TestCreate ()
		{
			AnnotationEntry entry;

			try {
				entry = AnnotationEntry.Create ("/comment");
			} catch (Exception ex) {
				Assert.Fail ($"Did not expect: {ex}");
				return;
			}
			Assert.That (entry, Is.EqualTo (AnnotationEntry.Comment), "/comment");

			try {
				entry = AnnotationEntry.Create ("/comment.priv");
			} catch (Exception ex) {
				Assert.Fail ($"Did not expect: {ex}");
				return;
			}
			Assert.That (entry, Is.EqualTo (AnnotationEntry.PrivateComment), "/comment.priv");

			try {
				entry = AnnotationEntry.Create ("/comment.shared");
			} catch (Exception ex) {
				Assert.Fail ($"Did not expect: {ex}");
				return;
			}
			Assert.That (entry, Is.EqualTo (AnnotationEntry.SharedComment), "/comment.shared");

			try {
				entry = AnnotationEntry.Create ("/flags");
			} catch (Exception ex) {
				Assert.Fail ($"Did not expect: {ex}");
				return;
			}
			Assert.That (entry, Is.EqualTo (AnnotationEntry.Flags), "/flags");

			try {
				entry = AnnotationEntry.Create ("/flags.priv");
			} catch (Exception ex) {
				Assert.Fail ($"Did not expect: {ex}");
				return;
			}
			Assert.That (entry, Is.EqualTo (AnnotationEntry.PrivateFlags), "/flags.priv");

			try {
				entry = AnnotationEntry.Create ("/flags.shared");
			} catch (Exception ex) {
				Assert.Fail ($"Did not expect: {ex}");
				return;
			}
			Assert.That (entry, Is.EqualTo (AnnotationEntry.SharedFlags), "/flags.shared");

			try {
				entry = AnnotationEntry.Create ("/altsubject");
			} catch (Exception ex) {
				Assert.Fail ($"Did not expect: {ex}");
				return;
			}
			Assert.That (entry, Is.EqualTo (AnnotationEntry.AltSubject), "/altsubject");

			try {
				entry = AnnotationEntry.Create ("/altsubject.priv");
			} catch (Exception ex) {
				Assert.Fail ($"Did not expect: {ex}");
				return;
			}
			Assert.That (entry, Is.EqualTo (AnnotationEntry.PrivateAltSubject), "/altsubject.priv");

			try {
				entry = AnnotationEntry.Create ("/altsubject.shared");
			} catch (Exception ex) {
				Assert.Fail ($"Did not expect: {ex}");
				return;
			}
			Assert.That (entry, Is.EqualTo (AnnotationEntry.SharedAltSubject), "/altsubject.shared");

			try {
				entry = AnnotationEntry.Create ("/1.2.3.4/comment");
			} catch (Exception ex) {
				Assert.Fail ($"Did not expect: {ex}");
				return;
			}
			Assert.That (entry.Entry, Is.EqualTo ("/1.2.3.4/comment"), "Entry");
			Assert.That (entry.PartSpecifier, Is.EqualTo ("1.2.3.4"), "PartSpecifier");
			Assert.That (entry.Path, Is.EqualTo ("/comment"), "Path");
			Assert.That (entry.Scope, Is.EqualTo (AnnotationScope.Both), "Scope");
		}
	}
}
