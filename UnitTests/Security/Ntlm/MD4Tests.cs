//
// MD4Tests.cs
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
using System.IO;
using System.Text;

using NUnit.Framework;

using MailKit.Security.Ntlm;

namespace UnitTests.Security.Ntlm {
	[TestFixture]
	public class MD4Tests
	{
		[Test]
		public void TestArgumentExceptions ()
		{
			using (var md4 = new MD4 ()) {
				var buffer = new byte[16];

				Assert.Throws<InvalidOperationException> (() => { var x = md4.Hash; });

				Assert.Throws<ArgumentNullException> (() => md4.ComputeHash ((byte[]) null));
				Assert.Throws<ArgumentNullException> (() => md4.ComputeHash ((Stream) null));
				Assert.Throws<ArgumentNullException> (() => md4.ComputeHash (null, 0, buffer.Length));
				Assert.Throws<ArgumentOutOfRangeException> (() => md4.ComputeHash (buffer, -1, buffer.Length));
				Assert.Throws<ArgumentOutOfRangeException> (() => md4.ComputeHash (buffer, 0, -1));

				Assert.Throws<ArgumentNullException> (() => md4.TransformBlock (null, 0, buffer.Length, buffer, 0));
				Assert.Throws<ArgumentOutOfRangeException> (() => md4.TransformBlock (buffer, -1, buffer.Length, buffer, 0));
				Assert.Throws<ArgumentOutOfRangeException> (() => md4.TransformBlock (buffer, 0, -1, buffer, 0));
				Assert.Throws<ArgumentOutOfRangeException> (() => md4.TransformBlock (buffer, 0, buffer.Length, buffer, -1));

				Assert.Throws<ArgumentNullException> (() => md4.TransformFinalBlock (null, 0, buffer.Length));
				Assert.Throws<ArgumentOutOfRangeException> (() => md4.TransformFinalBlock (buffer, -1, buffer.Length));
				Assert.Throws<ArgumentOutOfRangeException> (() => md4.TransformFinalBlock (buffer, 0, -1));
			}
		}

		[Test]
		public void TestSimpleInput ()
		{
			var text = Encoding.ASCII.GetBytes ("This is some sample text that we will hash using the MD4 algorithm.");
			const string expected = "69b390afdf693eae92ebea5cc6669b3f";

			using (var md4 = new MD4 ()) {
				StringBuilder builder = new StringBuilder ();
				byte[] hash, output = new byte[16];

				Assert.AreEqual (16, md4.TransformBlock (text, 0, 16, output, 0), "TransformBlock");
				output = md4.TransformFinalBlock (text, 16, text.Length - 16);
				Assert.NotNull (output, "TransformFinalBlock");
				Assert.AreEqual (text.Length - 16, output.Length, "TransformFinalBlock");
				hash = md4.Hash;
				Assert.NotNull (hash, "Hash");
				for (int i = 0; i < hash.Length; i++)
					builder.Append (hash[i].ToString ("x2"));
				Assert.AreEqual (expected, builder.ToString (), "Hash");
			}

			using (var md4 = new MD4 ()) {
				StringBuilder builder = new StringBuilder ();

				var hash = md4.ComputeHash (text);
				Assert.NotNull (hash, "ComputeHash");
				for (int i = 0; i < hash.Length; i++)
					builder.Append (hash[i].ToString ("x2"));
				Assert.AreEqual (expected, builder.ToString (), "ComputeHash");
			}

			using (var md4 = new MD4 ()) {
				StringBuilder builder = new StringBuilder ();
				byte[] hash;

				using (var stream = new MemoryStream (text, false))
					hash = md4.ComputeHash (stream);
				Assert.NotNull (hash, "ComputeHash");
				for (int i = 0; i < hash.Length; i++)
					builder.Append (hash[i].ToString ("x2"));
				Assert.AreEqual (expected, builder.ToString (), "ComputeHash");
			}
		}
	}
}
