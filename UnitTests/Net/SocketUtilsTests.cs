//
// SocketUtilsTests.cs
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

using System.Diagnostics;

using MailKit.Net;

namespace UnitTests.Net {
	[TestFixture]
	public class SocketUtilsTests
	{
		[Test]
		public void TestConnectTimeout ()
		{
			var stopwatch = new Stopwatch ();

			stopwatch.Start ();

			try {
				SocketUtils.Connect ("smtp.gmail.com", 466, null, 10000, CancellationToken.None);
				Assert.Fail ("Expected OperationCanceledException to be thrown.");
			} catch (TimeoutException) {
				stopwatch.Stop ();

				var elapsed = stopwatch.Elapsed.TotalSeconds;

				if (elapsed < 9.5 || elapsed > 12)
					Assert.Fail ($"Expected timeout to be around 10 seconds, but was {elapsed} seconds.");
				else
					Assert.Pass ("Connect timed out as expected.");
			} catch (Exception ex) {
				Assert.Fail ($"Expected TimeoutException to be thrown, but got: {ex}");
			}
		}

		[Test]
		public async Task TestConnectTimeoutAsync ()
		{
			var stopwatch = new Stopwatch ();

			stopwatch.Start ();

			try {
				await SocketUtils.ConnectAsync ("smtp.gmail.com", 466, null, 10000, CancellationToken.None);
				Assert.Fail ("Expected OperationCanceledException to be thrown.");
			} catch (TimeoutException) {
				stopwatch.Stop ();

				var elapsed = stopwatch.Elapsed.TotalSeconds;

				if (elapsed < 10 || elapsed > 12)
					Assert.Fail ($"Expected timeout to be around 10 seconds, but was {elapsed} seconds.");
				else
					Assert.Pass ("ConnectAsync timed out as expected.");
			} catch (Exception ex) {
				Assert.Fail ($"Expected TimeoutException to be thrown, but got: {ex}");
			}
		}
	}
}
