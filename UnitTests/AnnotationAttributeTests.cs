//
// AnnotationAttributeTests.cs
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
			Assert.AreEqual ("value", attr.Name, "Name");
			Assert.AreEqual ("value", attr.Specifier, "Specifier");
			Assert.AreEqual (AnnotationScope.Both, attr.Scope, "Scope");

			attr = new AnnotationAttribute ("value.priv");
			Assert.AreEqual ("value", attr.Name, "Name");
			Assert.AreEqual ("value.priv", attr.Specifier, "Specifier");
			Assert.AreEqual (AnnotationScope.Private, attr.Scope, "Scope");

			attr = new AnnotationAttribute ("value.shared");
			Assert.AreEqual ("value", attr.Name, "Name");
			Assert.AreEqual ("value.shared", attr.Specifier, "Specifier");
			Assert.AreEqual (AnnotationScope.Shared, attr.Scope, "Scope");
		}

		[Test]
		public void TestEquality ()
		{
			var value = new AnnotationAttribute ("value");

			Assert.AreEqual (AnnotationAttribute.Value, value, "AreEqual");
			Assert.IsTrue (AnnotationAttribute.Value.Equals (value), ".Equals");
			Assert.IsTrue (value == AnnotationAttribute.Value, "value == value");
			Assert.IsTrue (AnnotationAttribute.PrivateValue != AnnotationAttribute.SharedValue, "value.priv != value.shared");

			Assert.IsFalse (AnnotationAttribute.Value.Equals ((object) null), "value.Equals ((object) null)");
			Assert.IsFalse (AnnotationAttribute.Value.Equals ((AnnotationAttribute) null), "value.Equals ((AnnotationAttribute) null)");
			Assert.IsFalse (AnnotationAttribute.Value == null, "value == null");
			Assert.IsTrue (AnnotationAttribute.Value != null, "/comment != null");
		}
	}
}
