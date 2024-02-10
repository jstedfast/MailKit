//
// MD4Tests.cs
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

				Assert.That (md4.TransformBlock (text, 0, 16, output, 0), Is.EqualTo (16), "TransformBlock");
				output = md4.TransformFinalBlock (text, 16, text.Length - 16);
				Assert.That (output, Is.Not.Null, "TransformFinalBlock");
				Assert.That (output, Has.Length.EqualTo (text.Length - 16), "TransformFinalBlock");
				hash = md4.Hash;
				Assert.That (hash, Is.Not.Null, "Hash");
				for (int i = 0; i < hash.Length; i++)
					builder.Append (hash[i].ToString ("x2"));
				Assert.That (builder.ToString (), Is.EqualTo (expected), "Hash");
			}

			using (var md4 = new MD4 ()) {
				StringBuilder builder = new StringBuilder ();

				var hash = md4.ComputeHash (text);
				Assert.That (hash, Is.Not.Null, "ComputeHash");
				for (int i = 0; i < hash.Length; i++)
					builder.Append (hash[i].ToString ("x2"));
				Assert.That (builder.ToString (), Is.EqualTo (expected), "ComputeHash");
			}

			using (var md4 = new MD4 ()) {
				StringBuilder builder = new StringBuilder ();
				byte[] hash;

				using (var stream = new MemoryStream (text, false))
					hash = md4.ComputeHash (stream);
				Assert.That (hash, Is.Not.Null, "ComputeHash");
				for (int i = 0; i < hash.Length; i++)
					builder.Append (hash[i].ToString ("x2"));
				Assert.That (builder.ToString (), Is.EqualTo (expected), "ComputeHash");
			}
		}
	}
}
