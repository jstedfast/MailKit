//
// CryptographySmokeTest.cs
//
// Author: Jeffrey Stedfast <jestedfa@microsoft.com>
//
// Copyright (c) 2013-2026 .NET Foundation and Contributors
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
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

using MimeKit;
using MimeKit.Cryptography;

using X509KeyUsageFlags = System.Security.Cryptography.X509Certificates.X509KeyUsageFlags;

namespace PackageSmokeTests {
	static class CryptographySmokeTest
	{
		public static void Run ()
		{
			CryptographyModule.Initialize ();

			using var rsa = RSA.Create (2048);
			var request = new CertificateRequest ("CN=Package Smoke Test, E=sender@example.com", rsa,
				HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
			request.CertificateExtensions.Add (new X509KeyUsageExtension (X509KeyUsageFlags.DigitalSignature, true));

			using var certificate = request.CreateSelfSigned (DateTimeOffset.UtcNow.AddMinutes (-1), DateTimeOffset.UtcNow.AddDays (1));
			using var context = new TemporarySecureMimeContext ();
			context.CheckCertificateRevocation = false;

			var signer = new CmsSigner (certificate) { DigestAlgorithm = DigestAlgorithm.Sha256 };
			context.Import (signer.Certificate, trusted: true);
			using var body = new TextPart ("plain") { Text = "Signed package smoke test." };
			using var signed = MultipartSigned.Create (context, signer, body);
			using var stream = new MemoryStream ();
			signed.WriteTo (stream);
			stream.Position = 0;

			using var parsed = MimeEntity.Load (stream);
			Program.Require (parsed is MultipartSigned, "Cryptography module did not register multipart/signed.");
			var signatures = ((MultipartSigned) parsed).Verify (context);
			Program.Require (signatures.Count == 1 && signatures[0].Verify (), "S/MIME signature verification failed.");
			Console.WriteLine ("Cryptography module registration and S/MIME sign/verify passed.");
		}
	}
}
