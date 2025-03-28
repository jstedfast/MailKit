﻿//
// SaslMechanismOAuth2Tests.cs
//
// Author: Jeffrey Stedfast <jestedfa@microsoft.com>
//
// Copyright (c) 2013-2025 .NET Foundation and Contributors
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

using System.Net;

using MailKit.Security;

namespace UnitTests.Security {
	[TestFixture]
	public class SaslMechanismOAuth2Tests
	{
		[Test]
		public void TestArgumentExceptions ()
		{
			var credentials = new NetworkCredential ("username", "password");

			Assert.Throws<ArgumentNullException> (() => new SaslMechanismOAuth2 (null));
			Assert.Throws<ArgumentNullException> (() => new SaslMechanismOAuth2 (null, "password"));
			Assert.Throws<ArgumentNullException> (() => new SaslMechanismOAuth2 ("username", null));
		}

		static void AssertSimpleOAuth2 (SaslMechanismOAuth2 sasl, string prefix)
		{
			const string expected = "dXNlcj11c2VybmFtZQFhdXRoPUJlYXJlciBwYXNzd29yZAEB";
			string challenge;

			Assert.That (sasl.SupportsInitialResponse, Is.True, $"{prefix}: SupportsInitialResponse");
			challenge = sasl.Challenge (string.Empty);
			Assert.That (sasl.IsAuthenticated, Is.True, $"{prefix}: IsAuthenticated");
			Assert.That (challenge, Is.EqualTo (expected), $"{prefix}: Challenge");
			Assert.That (sasl.Challenge (string.Empty), Is.EqualTo (string.Empty), $"{prefix}: Already authenticated.");
		}

		[Test]
		public void TestSimpleOAuth2 ()
		{
			var credentials = new NetworkCredential ("username", "password");
			SaslMechanismOAuth2 sasl;

			sasl = new SaslMechanismOAuth2 (credentials);

			AssertSimpleOAuth2 (sasl, "NetworkCredential");

			sasl = new SaslMechanismOAuth2 ("username", "password");

			AssertSimpleOAuth2 (sasl, "user/pass");
		}
	}
}
