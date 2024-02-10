//
// NtlmSingleHostDataTests.cs
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

using MailKit.Security.Ntlm;

namespace UnitTests.Security.Ntlm {
	[TestFixture]
	public class NtlmSingleHostDataTests
	{
		[Test]
		public void TestArgumentExceptions ()
		{
			var customData = new byte[8];
			var machineId = new byte[32];
			var buffer = new byte[48];

			Assert.Throws<ArgumentNullException> (() => new NtlmSingleHostData (null, machineId));
			Assert.Throws<ArgumentException> (() => new NtlmSingleHostData (machineId, machineId));
			Assert.Throws<ArgumentNullException> (() => new NtlmSingleHostData (customData, null));
			Assert.Throws<ArgumentException> (() => new NtlmSingleHostData (customData, customData));

			Assert.Throws<ArgumentNullException> (() => new NtlmSingleHostData (null, 0, 48));
			Assert.Throws<ArgumentOutOfRangeException> (() => new NtlmSingleHostData (buffer, -1, 48));
			Assert.Throws<ArgumentOutOfRangeException> (() => new NtlmSingleHostData (buffer, 0, 25));
		}
	}
}
