//
// TestHelper.cs
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

using System.Text;

using NUnit.Framework;

namespace UnitTests {
	[SetUpFixture]
	static class TestHelper
	{
		public static readonly string ProjectDir;

		static TestHelper ()
		{
#if NET5_0_OR_GREATER
			var codeBase = typeof (TestHelper).Assembly.Location;
#else
			var codeBase = typeof (TestHelper).Assembly.CodeBase;
			if (codeBase.StartsWith ("file://", StringComparison.OrdinalIgnoreCase))
				codeBase = codeBase.Substring ("file://".Length);

			if (Path.DirectorySeparatorChar == '\\') {
				if (codeBase[0] == '/')
					codeBase = codeBase.Substring (1);

				codeBase = codeBase.Replace ('/', '\\');
			}
#endif

			var dir = Path.GetDirectoryName (codeBase);

			while (Path.GetFileName (dir) != "UnitTests")
				dir = Path.GetFullPath (Path.Combine (dir, ".."));

			ProjectDir = Path.GetFullPath (dir);
		}

#if NET5_0_OR_GREATER
		static void OnUnobservedTaskException (object sender, UnobservedTaskExceptionEventArgs e)
		{
			Console.WriteLine ("Unobserved Task Exception:");
			Console.WriteLine (e.Exception);
		}

		[OneTimeSetUp]
		public static void Init ()
		{
			lock (CodePagesEncodingProvider.Instance)
				Encoding.RegisterProvider (CodePagesEncodingProvider.Instance);

			TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
		}
#endif
	}
}
