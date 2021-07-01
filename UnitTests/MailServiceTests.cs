//
// MailServiceTests.cs
//
// Author: Jeffrey Stedfast <jestedfa@microsoft.com>
//
// Copyright (c) 2013-2021 .NET Foundation and Contributors
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
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;

using NUnit.Framework;

using MailKit;
using MailKit.Security;
using MailKit.Net.Imap;
using MailKit.Net.Pop3;
using MailKit.Net.Smtp;

namespace UnitTests {
	[TestFixture]
	public class MailServiceTests
	{
		bool SslCertificateValidationCallback (object sender, X509Certificate certificate, X509Chain chain, SslPolicyErrors sslPolicyErrors)
		{
			var certificate2 = certificate as X509Certificate2;
			var cn = certificate2.GetNameInfo (X509NameType.SimpleName, false);
			var fingerprint = certificate2.Thumbprint;
			var serial = certificate2.SerialNumber;
			var issuer = certificate2.Issuer;
			var expires = certificate2.NotAfter;

			Assert.IsNotNull (certificate2, "Cast");
			Assert.IsTrue (MailService.IsKnownMailServerCertificate (certificate2), $"IsKnownMailServerCertificate failed: {cn} issuer={issuer} serial={serial} fingerprint={fingerprint} // Expires {expires}");

			return true;
		}

		[Test]
		public void TestIsKnownMailServerCertificate ()
		{
			var servers = new string[] {
				"imap://imap.gmail.com:993",
				"pop://pop.gmail.com:995",
				"smtp://smtp.gmail.com:587",

				"imap://imap-mail.outlook.com:993",
				"pop://pop-mail.outlook.com:995",
				"smtp://smtp-mail.outlook.com:587",

				"imap://outlook.office365.com:993",
				"pop://outlook.office365.com:995",
				"smtp://smtp.office365.com:587",

				"imap://imap.mail.me.com:993",
				"smtp://smtp.mail.me.com:587",

				"imap://imap.mail.yahoo.com:993",
				"pop://pop.mail.yahoo.com:995",
				"smtp://smtp.mail.yahoo.com:587",

				"imap://imap.gmx.com:993",
				"pop://pop.gmx.com:995",
				"smtp://mail.gmx.com:465",

				"imap://imap.gmx.de:993",
				"pop://pop.gmx.de:995",
				"smtp://mail.gmx.de:465"
			};

			foreach (var server in servers) {
				var uri = new Uri (server);
				MailService client;

				switch (uri.Scheme) {
				case "imap": client = new ImapClient (); break;
				case "pop": client = new Pop3Client (); break;
				case "smtp": client = new SmtpClient (); break;
				default: throw new Exception ("Unsupported protocol");
				}

				using (client) {
					client.ServerCertificateValidationCallback = SslCertificateValidationCallback;
					try {
						client.Connect (uri.Host, uri.Port, SecureSocketOptions.Auto);
					} catch {
						continue;
					}
					client.Disconnect (true);
				}
			}
		}
	}
}
