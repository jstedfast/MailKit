//
// MessageThreadingTests.cs
//
// Author: Jeffrey Stedfast <jeff@xamarin.com>
//
// Copyright (c) 2014 Xamarin Inc. (www.xamarin.com)
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

using NUnit.Framework;

using MailKit;

namespace UnitTests {
	[TestFixture]
	public class MessageThreadingTests
	{
		[Test]
		public void TestThreadableSubject ()
		{
			string result;
			int depth;

			result = MessageThreader.GetThreadableSubject ("Re: simple subject", out depth);
			Assert.AreEqual ("simple subject", result, "#1a");
			Assert.AreEqual (1, depth, "#1b");

			result = MessageThreader.GetThreadableSubject ("Re: simple subject  ", out depth);
			Assert.AreEqual ("simple subject", result, "#2a");
			Assert.AreEqual (1, depth, "#2b");

			result = MessageThreader.GetThreadableSubject ("Re: Re: simple subject  ", out depth);
			Assert.AreEqual ("simple subject", result, "#3a");
			Assert.AreEqual (2, depth, "#3b");

			result = MessageThreader.GetThreadableSubject ("Re: Re[4]: simple subject  ", out depth);
			Assert.AreEqual ("simple subject", result, "#4a");
			Assert.AreEqual (5, depth, "#4b");

			result = MessageThreader.GetThreadableSubject ("Re: [Mailing-List] Re[4]: simple subject  ", out depth);
			Assert.AreEqual ("simple subject", result, "#5a");
			Assert.AreEqual (5, depth, "#5b");
		}

		// FIXME: implement some actual threading tests
	}
}
