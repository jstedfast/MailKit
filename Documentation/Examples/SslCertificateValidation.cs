using System;
using System.Net.Security;
using System.Collections.Generic;
using System.Security.Cryptography.X509Certificates;

using MimeKit;
using MailKit;
using MailKit.Security;
using MailKit.Net.Smtp;

namespace MailKit.Examples
{
	public static class SslCertificateValidationExample
	{
		public static void SendMessage (MimeMessage message)
		{
			using (var client = new SmtpClient ()) {
				// Set our custom SSL certificate validation callback.
				client.ServerCertificateValidationCallback = MySslCertificateValidationCallback;

				// Connect to smtp.gmail.com on the SSL-wrapped port.
				client.Connect ("smtp.gmail.com", 465, SecureSocketOptions.SslOnConnect);

				// Authenticate with our username and password.
				client.Authenticate ("username@gmail.com", "password");

				// Send our message.
				client.Send (message);

				// Disconnect cleanly from the server.
				client.Disconnect (true);
			}
		}

		static bool MySslCertificateValidationCallback (object sender, X509Certificate certificate, X509Chain chain, SslPolicyErrors sslPolicyErrors)
		{
			// If there are no errors, then everything went smoothly.
			if (sslPolicyErrors == SslPolicyErrors.None)
				return true;

			// Note: MailKit will always pass the host name string as the `sender` argument.
			var host = (string) sender;

			if ((sslPolicyErrors & SslPolicyErrors.RemoteCertificateNotAvailable) != 0) {
				// This means that the remote certificate is unavailable. Notify the user and return false.
				Console.WriteLine ("The SSL certificate was not available for {0}", host);
				return false;
			}

			if ((sslPolicyErrors & SslPolicyErrors.RemoteCertificateNameMismatch) != 0) {
				// This means that the server's SSL certificate did not match the host name that we are trying to connect to.
				var certificate2 = certificate as X509Certificate2;
				var cn = certificate2 != null ? certificate2.GetNameInfo (X509NameType.SimpleName, false) : certificate.Subject;

				Console.WriteLine ("The Common Name for the SSL certificate did not match {0}. Instead, it was {1}.", host, cn);
				return false;
			}

			// The only other errors left are chain errors.
			Console.WriteLine ("The SSL certificate for the server could not be validated for the following reasons:");

			// The first element's certificate will be the server's SSL certificate (and will match the `certificate` argument)
			// while the last element in the chain will typically either be the Root Certificate Authority's certificate -or- it
			// will be a non-authoritative self-signed certificate that the server admin created. 
			foreach (var element in chain.ChainElements) {
				// Each element in the chain will have its own status list. If the status list is empty, it means that the
				// certificate itself did not contain any errors.
				if (element.ChainElementStatus.Length == 0)
					continue;

				Console.WriteLine ("\u2022 {0}", element.Certificate.Subject);
				foreach (var error in element.ChainElementStatus) {
					// `error.StatusInformation` contains a human-readable error string while `error.Status` is the corresponding enum value.
					Console.WriteLine ("\t\u2022 {0}", error.StatusInformation);
				}
			}

			return false;
		}
	}
}
