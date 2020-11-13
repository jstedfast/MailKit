//
// SaslMechanismLoginTests.cs
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
using System.Net;
using System.Text;

using NUnit.Framework;

using MailKit.Security;

namespace UnitTests.Security {
	[TestFixture]
	public class SaslMechanismLoginTests
	{
		[Test]
		public void TestArgumentExceptions ()
		{
			var credentials = new NetworkCredential ("username", "password");
			var uri = new Uri ("smtp://localhost");

			var sasl = new SaslMechanismLogin (credentials);
			Assert.Throws<NotSupportedException> (() => sasl.Challenge (null));

			Assert.Throws<ArgumentNullException> (() => new SaslMechanismLogin ((Uri) null, Encoding.UTF8, credentials));
			Assert.Throws<ArgumentNullException> (() => new SaslMechanismLogin (uri, null, credentials));
			Assert.Throws<ArgumentNullException> (() => new SaslMechanismLogin (uri, Encoding.UTF8, null));
			Assert.Throws<ArgumentNullException> (() => new SaslMechanismLogin ((Uri) null, credentials));
			Assert.Throws<ArgumentNullException> (() => new SaslMechanismLogin (uri, null));
			Assert.Throws<ArgumentNullException> (() => new SaslMechanismLogin ((Uri) null, Encoding.UTF8, "username", "password"));
			Assert.Throws<ArgumentNullException> (() => new SaslMechanismLogin (uri, null, "username", "password"));
			Assert.Throws<ArgumentNullException> (() => new SaslMechanismLogin (uri, Encoding.UTF8, null, "password"));
			Assert.Throws<ArgumentNullException> (() => new SaslMechanismLogin (uri, Encoding.UTF8, "username", null));
			Assert.Throws<ArgumentNullException> (() => new SaslMechanismLogin ((Uri) null, "username", "password"));
			Assert.Throws<ArgumentNullException> (() => new SaslMechanismLogin (uri, null, "password"));
			Assert.Throws<ArgumentNullException> (() => new SaslMechanismLogin (uri, "username", null));
			Assert.Throws<ArgumentNullException> (() => new SaslMechanismLogin (null, credentials));
			Assert.Throws<ArgumentNullException> (() => new SaslMechanismLogin (Encoding.UTF8, null));
			Assert.Throws<ArgumentNullException> (() => new SaslMechanismLogin (null));
			Assert.Throws<ArgumentNullException> (() => new SaslMechanismLogin ((Encoding) null, "username", "password"));
			Assert.Throws<ArgumentNullException> (() => new SaslMechanismLogin (Encoding.UTF8, null, "password"));
			Assert.Throws<ArgumentNullException> (() => new SaslMechanismLogin (Encoding.UTF8, "username", null));
			Assert.Throws<ArgumentNullException> (() => new SaslMechanismLogin (null, "password"));
			Assert.Throws<ArgumentNullException> (() => new SaslMechanismLogin ("username", null));
		}

		static void AssertLogin (SaslMechanismLogin sasl, string prefix)
		{
			const string expected1 = "dXNlcm5hbWU=";
			const string expected2 = "cGFzc3dvcmQ=";
			string challenge;

			Assert.IsFalse (sasl.SupportsInitialResponse, "{0}: SupportsInitialResponse", prefix);

			challenge = sasl.Challenge (string.Empty);

			Assert.AreEqual (expected1, challenge, "{0}: initial challenge response does not match the expected string.", prefix);
			Assert.IsFalse (sasl.IsAuthenticated, "{0}: should not be authenticated.", prefix);

			challenge = sasl.Challenge (string.Empty);

			Assert.AreEqual (expected2, challenge, "{0}: final challenge response does not match the expected string.", prefix);
			Assert.IsTrue (sasl.IsAuthenticated, "{0}: should be authenticated.", prefix);
			Assert.AreEqual (string.Empty, sasl.Challenge (string.Empty), "{0}: challenge while authenticated.", prefix);
		}

		[Test]
		public void TestLoginAuth ()
		{
			var credentials = new NetworkCredential ("username", "password");
			var sasl = new SaslMechanismLogin (credentials);
			var uri = new Uri ("smtp://localhost");

			AssertLogin (sasl, "NetworkCredential");

			sasl = new SaslMechanismLogin ("username", "password");

			AssertLogin (sasl, "user/pass");

			sasl = new SaslMechanismLogin (uri, credentials);

			AssertLogin (sasl, "uri/credentials");

			sasl = new SaslMechanismLogin (uri, "username", "password");

			AssertLogin (sasl, "uri/user/pass");
		}
	}
}
