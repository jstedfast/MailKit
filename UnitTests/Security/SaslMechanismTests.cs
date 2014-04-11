//
// CramMd5Tests.cs
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

using System;
using System.Net;
using System.Text;

using NUnit.Framework;

using MailKit.Security;

namespace UnitTests.Security {
	[TestFixture]
	public class SaslMechanismTests
	{
		[Test]
		public void TestCramMd5ExampleFromRfc2195 ()
		{
			const string serverToken = "<1896.697170952@postoffice.example.net>";
			const string expected = "joe 3dbc88f0624776a737b39093f6eb6427";
			var credentials = new NetworkCredential ("joe", "tanstaaftanstaaf");
			var uri = new Uri ("imap://imap.gmail.com");
			var sasl = new SaslMechanismCramMd5 (uri, credentials);

			var token = Encoding.ASCII.GetBytes (serverToken);
			var challenge = sasl.Challenge (Convert.ToBase64String (token));
			var decoded = Convert.FromBase64String (challenge);
			var result = Encoding.ASCII.GetString (decoded);

			Assert.AreEqual (expected, result, "CRAM-MD5 challenge response does not match the expected string.");
			Assert.IsTrue (sasl.IsAuthenticated, "CRAM-MD5 should be authenticated.");
		}

		[Test]
		public void TestDigestMd5ExampleFromRfc2831 ()
		{
			const string serverToken1 = "realm=\"elwood.innosoft.com\",nonce=\"OA6MG9tEQGm2hh\",qop=\"auth\",algorithm=md5-sess,charset=utf-8";
			const string expected1 = "username=\"chris\",realm=\"elwood.innosoft.com\",nonce=\"OA6MG9tEQGm2hh\",cnonce=\"OA6MHXh6VqTrRk\",nc=00000001,qop=\"auth\",digest-uri=\"imap/elwood.innosoft.com\",response=\"d388dad90d4bbd760a152321f2143af7\",charset=\"utf-8\",algorithm=\"md5-sess\"";
			const string serverToken2 = "rspauth=ea40f60335c427b5527b84dbabcdfffd";
			var credentials = new NetworkCredential ("chris", "secret");
			var uri = new Uri ("imap://elwood.innosoft.com");
			var sasl = new SaslMechanismDigestMd5 (uri, credentials, "OA6MHXh6VqTrRk");

			var token = Encoding.ASCII.GetBytes (serverToken1);
			var challenge = sasl.Challenge (Convert.ToBase64String (token));
			var decoded = Convert.FromBase64String (challenge);
			var result = Encoding.ASCII.GetString (decoded);

			Assert.AreEqual (expected1, result, "DIGEST-MD5 challenge response does not match the expected string.");
			Assert.IsFalse (sasl.IsAuthenticated, "DIGEST-MD5 should not be authenticated yet.");

			token = Encoding.ASCII.GetBytes (serverToken2);
			challenge = sasl.Challenge (Convert.ToBase64String (token));
			decoded = Convert.FromBase64String (challenge);
			result = Encoding.ASCII.GetString (decoded);

			Assert.AreEqual (string.Empty, result, "Second DIGEST-MD5 challenge should be an empty string.");
			Assert.IsTrue (sasl.IsAuthenticated, "DIGEST-MD5 should be authenticated now.");
		}
	}
}
