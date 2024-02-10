//
// ReplaceRequestTests.cs
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

using MimeKit;
using MailKit;

namespace UnitTests {
	[TestFixture]
	public class ReplaceRequestTests
	{
		[Test]
		public void TestArgumentExceptions ()
		{
			var keywords = new string[] { "$Forwarded" };
			var message = new MimeMessage ();

			Assert.Throws<ArgumentNullException> (() => new ReplaceRequest (null));
			Assert.Throws<ArgumentNullException> (() => new ReplaceRequest (null, MessageFlags.Seen));
			Assert.Throws<ArgumentNullException> (() => new ReplaceRequest (null, MessageFlags.Seen, DateTimeOffset.Now));
			Assert.Throws<ArgumentNullException> (() => new ReplaceRequest (null, MessageFlags.Seen, keywords));
			Assert.Throws<ArgumentNullException> (() => new ReplaceRequest (message, MessageFlags.Seen, null));
			Assert.Throws<ArgumentNullException> (() => new ReplaceRequest (null, MessageFlags.Seen, keywords, DateTimeOffset.Now));
			Assert.Throws<ArgumentNullException> (() => new ReplaceRequest (message, MessageFlags.Seen, null, DateTimeOffset.Now));
		}

		[Test]
		public void TestConstructors ()
		{
			//var annotation = new Annotation (AnnotationEntry.AltSubject);
			//annotation.Properties[AnnotationAttribute.PrivateValue] = string.Format ("Alternate subject");
			//var annotations = new Annotation[] { annotation };
			var keywords = new string[] { "$Forwarded", "$Junk" };
			var keywordSet = new HashSet<string> (keywords);
			var flags = MessageFlags.Seen | MessageFlags.Draft;
			var internalDate = DateTimeOffset.Now;
			var message = new MimeMessage ();
			ReplaceRequest request;

			request = new ReplaceRequest (message);
			Assert.That (request.Message, Is.EqualTo (message), "Message #1");
			Assert.That (request.Flags, Is.EqualTo (MessageFlags.None), "Flags #1");
			Assert.That (request.Keywords, Is.Null, "Keywords #1");
			Assert.That (request.InternalDate, Is.Null, "InternalDate #1");
			Assert.That (request.Annotations, Is.Null, "Annotations #1");

			request = new ReplaceRequest (message, flags);
			Assert.That (request.Message, Is.EqualTo (message), "Message #2");
			Assert.That (request.Flags, Is.EqualTo (flags), "Flags #2");
			Assert.That (request.Keywords, Is.Null, "Keywords #2");
			Assert.That (request.InternalDate, Is.Null, "InternalDate #2");
			Assert.That (request.Annotations, Is.Null, "Annotations #2");

			request = new ReplaceRequest (message, flags, keywords);
			Assert.That (request.Message, Is.EqualTo (message), "Message #3");
			Assert.That (request.Flags, Is.EqualTo (flags), "Flags #3");
			Assert.That (request.Keywords, Is.InstanceOf<HashSet<string>> (), "Keywords Type #3");
			Assert.That (request.Keywords, Has.Count.EqualTo (keywords.Length), "Keywords #3");
			Assert.That (request.InternalDate, Is.Null, "InternalDate #3");
			Assert.That (request.Annotations, Is.Null, "Annotations #3");

			request = new ReplaceRequest (message, flags, keywordSet);
			Assert.That (request.Message, Is.EqualTo (message), "Message #4");
			Assert.That (request.Flags, Is.EqualTo (flags), "Flags #4");
			Assert.That (request.Keywords, Is.InstanceOf<HashSet<string>> (), "Keywords Type #4");
			Assert.That (request.Keywords, Is.EqualTo (keywordSet), "Keywords #4");
			Assert.That (request.InternalDate, Is.Null, "InternalDate #4");
			Assert.That (request.Annotations, Is.Null, "Annotations #4");

			request = new ReplaceRequest (message, flags, internalDate);
			Assert.That (request.Message, Is.EqualTo (message), "Message #5");
			Assert.That (request.Flags, Is.EqualTo (flags), "Flags #5");
			Assert.That (request.Keywords, Is.Null, "Keywords #5");
			Assert.That (request.InternalDate.Value, Is.EqualTo (internalDate), "InternalDate #5");
			Assert.That (request.Annotations, Is.Null, "Annotations #5");

			request = new ReplaceRequest (message, flags, keywords, internalDate);
			Assert.That (request.Message, Is.EqualTo (message), "Message #6");
			Assert.That (request.Flags, Is.EqualTo (flags), "Flags #6");
			Assert.That (request.Keywords, Is.InstanceOf<HashSet<string>> (), "Keywords Type #6");
			Assert.That (request.Keywords, Has.Count.EqualTo (keywords.Length), "Keywords #6");
			Assert.That (request.InternalDate.Value, Is.EqualTo (internalDate), "InternalDate #6");
			Assert.That (request.Annotations, Is.Null, "Annotations #6");

			request = new ReplaceRequest (message, flags, keywordSet, internalDate);
			Assert.That (request.Message, Is.EqualTo (message), "Message #7");
			Assert.That (request.Flags, Is.EqualTo (flags), "Flags #7");
			Assert.That (request.Keywords, Is.InstanceOf<HashSet<string>> (), "Keywords Type #7");
			Assert.That (request.Keywords, Is.EqualTo (keywordSet), "Keywords #7");
			Assert.That (request.InternalDate.Value, Is.EqualTo (internalDate), "InternalDate #7");
			Assert.That (request.Annotations, Is.Null, "Annotations #7");
		}
	}
}
