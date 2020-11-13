//
// SaslMechanismOAuthBearerTests.cs
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

using NUnit.Framework;

using MailKit.Security;

namespace UnitTests.Security {
	[TestFixture]
	public class SaslMechanismOAuthBearerTests
	{
		[Test]
		public void TestArgumentExceptions ()
		{
			var credentials = new NetworkCredential ("username", "password");
			var uri = new Uri ("smtp://localhost");

			Assert.Throws<ArgumentNullException> (() => new SaslMechanismOAuthBearer (null, credentials));
			Assert.Throws<ArgumentNullException> (() => new SaslMechanismOAuthBearer (uri, null));
			Assert.Throws<ArgumentNullException> (() => new SaslMechanismOAuthBearer (null, "username", "password"));
			Assert.Throws<ArgumentNullException> (() => new SaslMechanismOAuthBearer (uri, null, "password"));
			Assert.Throws<ArgumentNullException> (() => new SaslMechanismOAuthBearer (uri, "username", null));
			Assert.Throws<ArgumentNullException> (() => new SaslMechanismOAuthBearer (null));
			Assert.Throws<ArgumentNullException> (() => new SaslMechanismOAuthBearer (null, "password"));
			Assert.Throws<ArgumentNullException> (() => new SaslMechanismOAuthBearer ("username", null));
		}

		static void AssertImapExampleFromRfc7628 (SaslMechanismOAuthBearer sasl, string prefix)
		{
			const string expected = "bixhPXVzZXJAZXhhbXBsZS5jb20sAWhvc3Q9c2VydmVyLmV4YW1wbGUuY29tAXBvcnQ9MTQzAWF1dGg9QmVhcmVyIHZGOWRmdDRxbVRjMk52YjNSbGNrQmhiSFJoZG1semRHRXVZMjl0Q2c9PQEB";
			string challenge;

			Assert.IsTrue (sasl.SupportsInitialResponse, "SupportsInitialResponse");
			challenge = sasl.Challenge (string.Empty);
			Assert.IsTrue (sasl.IsAuthenticated, "IsAuthenticated");
			Assert.AreEqual (expected, challenge, "Challenge");
			Assert.AreEqual ("AQ==", sasl.Challenge (string.Empty), "Already authenticated.");
		}

		[Test]
		public void TestImapExampleFromRfc7628 ()
		{
			const string userName = "user@example.com";
			const string token = "vF9dft4qmTc2Nvb3RlckBhbHRhdmlzdGEuY29tCg==";
			var credentials = new NetworkCredential (userName, token);
			var uri = new Uri ("imap://server.example.com:143");
			SaslMechanismOAuthBearer sasl;

			sasl = new SaslMechanismOAuthBearer (credentials) { Uri = uri };

			AssertImapExampleFromRfc7628 (sasl, "NetworkCredential");

			sasl = new SaslMechanismOAuthBearer (userName, token) { Uri = uri };

			AssertImapExampleFromRfc7628 (sasl, "user/pass");

			sasl = new SaslMechanismOAuthBearer (uri, credentials);

			AssertImapExampleFromRfc7628 (sasl, "uri/credentials");

			sasl = new SaslMechanismOAuthBearer (uri, userName, token);

			AssertImapExampleFromRfc7628 (sasl, "uri/user/pass");
		}

		static void AssertImapFailureExampleFromRfc7628 (SaslMechanismOAuthBearer sasl, string prefix)
		{
			const string failureResponse = "eyJzdGF0dXMiOiJpbnZhbGlkX3Rva2VuIiwic2NvcGUiOiJleGFtcGxlX3Njb3BlIiwib3BlbmlkLWNvbmZpZ3VyYXRpb24iOiJodHRwczovL2V4YW1wbGUuY29tLy53ZWxsLWtub3duL29wZW5pZC1jb25maWd1cmF0aW9uIn0=";
			const string expected = "bixhPXVzZXJAZXhhbXBsZS5jb20sAWhvc3Q9c2VydmVyLmV4YW1wbGUuY29tAXBvcnQ9MTQzAWF1dGg9QmVhcmVyIHZGOWRmdDRxbVRjMk52YjNSbGNrQmhiSFJoZG1semRHRXVZMjl0Q2c9PQEB";
			string challenge;

			Assert.IsTrue (sasl.SupportsInitialResponse, "SupportsInitialResponse");
			challenge = sasl.Challenge (string.Empty);
			Assert.IsTrue (sasl.IsAuthenticated, "IsAuthenticated");
			Assert.AreEqual (expected, challenge, "Challenge");
			Assert.AreEqual ("AQ==", sasl.Challenge (failureResponse), "Failure response.");
		}

		[Test]
		public void TestImapFailureExampleFromRfc7628 ()
		{
			const string userName = "user@example.com";
			const string token = "vF9dft4qmTc2Nvb3RlckBhbHRhdmlzdGEuY29tCg==";
			var credentials = new NetworkCredential (userName, token);
			var uri = new Uri ("imap://server.example.com:143");
			SaslMechanismOAuthBearer sasl;

			sasl = new SaslMechanismOAuthBearer (credentials) { Uri = uri };

			AssertImapFailureExampleFromRfc7628 (sasl, "NetworkCredential");

			sasl = new SaslMechanismOAuthBearer (userName, token) { Uri = uri };

			AssertImapFailureExampleFromRfc7628 (sasl, "user/pass");

			sasl = new SaslMechanismOAuthBearer (uri, credentials);

			AssertImapFailureExampleFromRfc7628 (sasl, "uri/credentials");

			sasl = new SaslMechanismOAuthBearer (uri, userName, token);

			AssertImapFailureExampleFromRfc7628 (sasl, "uri/user/pass");
		}

		static void AssertSmtpExampleFromRfc7628 (SaslMechanismOAuthBearer sasl, string prefix)
		{
			const string expected = "bixhPXVzZXJAZXhhbXBsZS5jb20sAWhvc3Q9c2VydmVyLmV4YW1wbGUuY29tAXBvcnQ9NTg3AWF1dGg9QmVhcmVyIHZGOWRmdDRxbVRjMk52YjNSbGNrQmhiSFJoZG1semRHRXVZMjl0Q2c9PQEB";
			string challenge;

			Assert.IsTrue (sasl.SupportsInitialResponse, "SupportsInitialResponse");
			challenge = sasl.Challenge (string.Empty);
			Assert.IsTrue (sasl.IsAuthenticated, "IsAuthenticated");
			Assert.AreEqual (expected, challenge, "Challenge");
			Assert.AreEqual ("AQ==", sasl.Challenge (string.Empty), "Already authenticated.");
		}

		[Test]
		public void TestSmtpExampleFromRfc7628 ()
		{
			const string userName = "user@example.com";
			const string token = "vF9dft4qmTc2Nvb3RlckBhbHRhdmlzdGEuY29tCg==";
			var credentials = new NetworkCredential (userName, token);
			var uri = new Uri ("smtp://server.example.com:587");
			SaslMechanismOAuthBearer sasl;

			sasl = new SaslMechanismOAuthBearer (credentials) { Uri = uri };

			AssertSmtpExampleFromRfc7628 (sasl, "NetworkCredential");

			sasl = new SaslMechanismOAuthBearer (userName, token) { Uri = uri };

			AssertSmtpExampleFromRfc7628 (sasl, "user/pass");

			sasl = new SaslMechanismOAuthBearer (uri, credentials);

			AssertSmtpExampleFromRfc7628 (sasl, "uri/credentials");

			sasl = new SaslMechanismOAuthBearer (uri, userName, token);

			AssertSmtpExampleFromRfc7628 (sasl, "uri/user/pass");
		}

		static void AssertSmtpFailureExampleFromRfc7628 (SaslMechanismOAuthBearer sasl, string prefix)
		{
			const string failureResponse = "eyJzdGF0dXMiOiJpbnZhbGlkX3Rva2VuIiwic2NoZW1lcyI6ImJlYXJlciBtYWMiLCJzY29wZSI6Imh0dHBzOi8vbWFpbC5leGFtcGxlLmNvbS8ifQ==";
			const string expected = "bixhPXVzZXJAZXhhbXBsZS5jb20sAWhvc3Q9c2VydmVyLmV4YW1wbGUuY29tAXBvcnQ9NTg3AWF1dGg9QmVhcmVyIHZGOWRmdDRxbVRjMk52YjNSbGNrQmhiSFJoZG1semRHRXVZMjl0Q2c9PQEB";
			string challenge;

			Assert.IsTrue (sasl.SupportsInitialResponse, "SupportsInitialResponse");
			challenge = sasl.Challenge (string.Empty);
			Assert.IsTrue (sasl.IsAuthenticated, "IsAuthenticated");
			Assert.AreEqual (expected, challenge, "Challenge");
			Assert.AreEqual ("AQ==", sasl.Challenge (failureResponse), "Failure response.");
		}

		[Test]
		public void TestSmtpFailureExampleFromRfc7628 ()
		{
			const string userName = "user@example.com";
			const string token = "vF9dft4qmTc2Nvb3RlckBhbHRhdmlzdGEuY29tCg==";
			var credentials = new NetworkCredential (userName, token);
			var uri = new Uri ("smtp://server.example.com:587");
			SaslMechanismOAuthBearer sasl;

			sasl = new SaslMechanismOAuthBearer (credentials) { Uri = uri };

			AssertSmtpFailureExampleFromRfc7628 (sasl, "NetworkCredential");

			sasl = new SaslMechanismOAuthBearer (userName, token) { Uri = uri };

			AssertSmtpFailureExampleFromRfc7628 (sasl, "user/pass");

			sasl = new SaslMechanismOAuthBearer (uri, credentials);

			AssertSmtpFailureExampleFromRfc7628 (sasl, "uri/credentials");

			sasl = new SaslMechanismOAuthBearer (uri, userName, token);

			AssertSmtpFailureExampleFromRfc7628 (sasl, "uri/user/pass");
		}
	}
}
