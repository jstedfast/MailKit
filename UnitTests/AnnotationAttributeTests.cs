//
// AnnotationAttributeTests.cs
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
	public class AnnotationAttributeTests
	{
		[Test]
		public void TestArgumentExceptions ()
		{
			Assert.Throws<ArgumentNullException> (() => new AnnotationAttribute (null));
			Assert.Throws<ArgumentException> (() => new AnnotationAttribute (string.Empty));
			Assert.Throws<ArgumentException> (() => new AnnotationAttribute ("*"));
			Assert.Throws<ArgumentException> (() => new AnnotationAttribute ("%"));
			Assert.Throws<ArgumentException> (() => new AnnotationAttribute ("w*ldcard"));
			Assert.Throws<ArgumentException> (() => new AnnotationAttribute ("w%ldcard"));
		}

		[Test]
		public void TestBasicFunctionality ()
		{
			AnnotationAttribute attr;

			attr = new AnnotationAttribute ("value");
			Assert.That (attr.Name, Is.EqualTo ("value"), "Name");
			Assert.That (attr.Specifier, Is.EqualTo ("value"), "Specifier");
			Assert.That (attr.Scope, Is.EqualTo (AnnotationScope.Both), "Scope");

			attr = new AnnotationAttribute ("value.priv");
			Assert.That (attr.Name, Is.EqualTo ("value"), "Name");
			Assert.That (attr.Specifier, Is.EqualTo ("value.priv"), "Specifier");
			Assert.That (attr.Scope, Is.EqualTo (AnnotationScope.Private), "Scope");

			attr = new AnnotationAttribute ("value.shared");
			Assert.That (attr.Name, Is.EqualTo ("value"), "Name");
			Assert.That (attr.Specifier, Is.EqualTo ("value.shared"), "Specifier");
			Assert.That (attr.Scope, Is.EqualTo (AnnotationScope.Shared), "Scope");
		}

		[Test]
		public void TestEquality ()
		{
			var value = new AnnotationAttribute ("value");

			Assert.That (value, Is.EqualTo (AnnotationAttribute.Value), "AreEqual");
			Assert.That (AnnotationAttribute.Value.Equals (value), Is.True, ".Equals");
			Assert.That (value == AnnotationAttribute.Value, Is.True, "value == value");
			Assert.That (AnnotationAttribute.PrivateValue != AnnotationAttribute.SharedValue, Is.True, "value.priv != value.shared");

			Assert.That (AnnotationAttribute.Value.Equals ((object) null), Is.False, "value.Equals ((object) null)");
			Assert.That (AnnotationAttribute.Value.Equals ((AnnotationAttribute) null), Is.False, "value.Equals ((AnnotationAttribute) null)");
			Assert.That (AnnotationAttribute.Value == null, Is.False, "value == null");
			Assert.That (AnnotationAttribute.Value != null, Is.True, "/comment != null");
		}
	}
}
