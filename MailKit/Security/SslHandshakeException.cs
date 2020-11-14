//
// SslHandshakeException.cs
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
using System.Linq;
using System.Text;
using System.Net.Security;
using System.Globalization;
using System.Collections.Generic;
#if SERIALIZABLE
using System.Security;
using System.Runtime.Serialization;
#endif
using System.Security.Cryptography.X509Certificates;

namespace MailKit.Security
{
	/// <summary>
	/// The exception that is thrown when there is an error during the SSL/TLS handshake.
	/// </summary>
	/// <remarks>
	/// <para>The exception that is thrown when there is an error during the SSL/TLS handshake.</para>
	/// <para>When this exception occurrs, it typically means that the IMAP, POP3 or SMTP server that
	/// you are connecting to is using an SSL certificate that is either expired or untrusted by
	/// your system.</para>
	/// <para>Often times, mail servers will use self-signed certificates instead of using a certificate
	/// that has been signed by a trusted Certificate Authority. When your system is unable to validate
	/// the mail server's certificate because it is not signed by a known and trusted Certificate Authority,
	/// this exception will occur.</para>
	/// <para>You can work around this problem by supplying a custom <see cref="System.Net.Security.RemoteCertificateValidationCallback"/>
	/// and setting it on the client's <see cref="MailService.ServerCertificateValidationCallback"/> property.</para>
	/// <para>Most likely, you'll want to compare the thumbprint of the server's certificate with a known
	/// value and/or prompt the user to accept the certificate (similar to what you've probably seen web
	/// browsers do when they encounter untrusted certificates).</para>
	/// </remarks>
#if SERIALIZABLE
	[Serializable]
#endif
	public class SslHandshakeException : Exception
	{
		const string SslHandshakeHelpLink = "https://github.com/jstedfast/MailKit/blob/master/FAQ.md#SslHandshakeException";
		const string DefaultMessage = "An error occurred while attempting to establish an SSL or TLS connection.";

#if SERIALIZABLE
		/// <summary>
		/// Initializes a new instance of the <see cref="SslHandshakeException"/> class.
		/// </summary>
		/// <remarks>
		/// Creates a new <see cref="SslHandshakeException"/> from the seriaized data.
		/// </remarks>
		/// <param name="info">The serialization info.</param>
		/// <param name="context">The streaming context.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <paramref name="info"/> is <c>null</c>.
		/// </exception>
		protected SslHandshakeException (SerializationInfo info, StreamingContext context) : base (info, context)
		{
			var base64 = info.GetString ("ServerCertificate");

			if (base64 != null)
				ServerCertificate = new X509Certificate2 (Convert.FromBase64String (base64));

			base64 = info.GetString ("RootCertificateAuthority");

			if (base64 != null)
				RootCertificateAuthority = new X509Certificate2 (Convert.FromBase64String (base64));
		}
#endif

		/// <summary>
		/// Initializes a new instance of the <see cref="SslHandshakeException"/> class.
		/// </summary>
		/// <remarks>
		/// Creates a new <see cref="SslHandshakeException"/>.
		/// </remarks>
		/// <param name="message">The error message.</param>
		/// <param name="innerException">An inner exception.</param>
		public SslHandshakeException (string message, Exception innerException) : base (message, innerException)
		{
			HelpLink = SslHandshakeHelpLink;
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="SslHandshakeException"/> class.
		/// </summary>
		/// <remarks>
		/// Creates a new <see cref="SslHandshakeException"/>.
		/// </remarks>
		/// <param name="message">The error message.</param>
		public SslHandshakeException (string message) : base (message)
		{
			HelpLink = SslHandshakeHelpLink;
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="SslHandshakeException"/> class.
		/// </summary>
		/// <remarks>
		/// Creates a new <see cref="SslHandshakeException"/>.
		/// </remarks>
		public SslHandshakeException () : base (DefaultMessage)
		{
			HelpLink = SslHandshakeHelpLink;
		}

		/// <summary>
		/// Get the server's SSL certificate.
		/// </summary>
		/// <remarks>
		/// Gets the server's SSL certificate, if it is available.
		/// </remarks>
		/// <value>The server's SSL certificate.</value>
		public X509Certificate ServerCertificate {
			get; private set;
		}

		/// <summary>
		/// Get the certificate for the Root Certificate Authority.
		/// </summary>
		/// <remarks>
		/// Gets the certificate for the Root Certificate Authority, if it is available.
		/// </remarks>
		/// <value>The Root Certificate Authority certificate.</value>
		public X509Certificate RootCertificateAuthority {
			get; private set;
		}

#if SERIALIZABLE
		/// <summary>
		/// When overridden in a derived class, sets the <see cref="System.Runtime.Serialization.SerializationInfo"/>
		/// with information about the exception.
		/// </summary>
		/// <remarks>
		/// Sets the <see cref="System.Runtime.Serialization.SerializationInfo"/>
		/// with information about the exception.
		/// </remarks>
		/// <param name="info">The serialization info.</param>
		/// <param name="context">The streaming context.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <paramref name="info"/> is <c>null</c>.
		/// </exception>
		[SecurityCritical]
		public override void GetObjectData (SerializationInfo info, StreamingContext context)
		{
			base.GetObjectData (info, context);

			if (ServerCertificate != null)
				info.AddValue ("ServerCertificate", Convert.ToBase64String (ServerCertificate.GetRawCertData ()));
			else
				info.AddValue ("ServerCertificate", null, typeof (string));

			if (RootCertificateAuthority != null)
				info.AddValue ("RootCertificateAuthority", Convert.ToBase64String (RootCertificateAuthority.GetRawCertData ()));
			else
				info.AddValue ("RootCertificateAuthority", null, typeof (string));
		}
#endif

		internal static SslHandshakeException Create (MailService client, Exception ex, bool starttls, string protocol, string host, int port, int sslPort, params int[] standardPorts)
		{
			var message = new StringBuilder (DefaultMessage);
			var aggregate = ex as AggregateException;
			X509Certificate certificate = null;
			X509Certificate root = null;

			if (aggregate != null) {
				aggregate = aggregate.Flatten ();

				if (aggregate.InnerExceptions.Count == 1)
					ex = aggregate.InnerExceptions[0];
				else
					ex = aggregate;
			}

			message.AppendLine ();
			message.AppendLine ();

			var validationInfo = client?.SslCertificateValidationInfo;
			if (validationInfo != null) {
				client.SslCertificateValidationInfo = null;

				int rootIndex = validationInfo.ChainElements.Count - 1;
				if (rootIndex > 0)
					root = validationInfo.ChainElements[rootIndex].Certificate;
				certificate = validationInfo.Certificate;

				if ((validationInfo.SslPolicyErrors & SslPolicyErrors.RemoteCertificateNotAvailable) != 0) {
					message.AppendLine ("The SSL certificate for the server was not available.");
				} else if ((validationInfo.SslPolicyErrors & SslPolicyErrors.RemoteCertificateNameMismatch) != 0) {
					message.AppendLine ("The host name did not match the name given in the server's SSL certificate.");
				} else {
					message.AppendLine ("The server's SSL certificate could not be validated for the following reasons:");

					bool haveReason = false;

					for (int chainIndex = 0; chainIndex < validationInfo.ChainElements.Count; chainIndex++) {
						var element = validationInfo.ChainElements[chainIndex];

						if (element.ChainElementStatus == null || element.ChainElementStatus.Length == 0)
							continue;

						if (chainIndex == 0) {
							message.AppendLine ("\u2022 The server certificate has the following errors:");
						} else if (chainIndex == rootIndex) {
							message.AppendLine ("\u2022 The root certificate has the following errors:");
						} else {
							message.AppendLine ("\u2022 An intermediate certificate has the following errors:");
						}

						foreach (var status in element.ChainElementStatus)
							message.AppendFormat ("  \u2022 {0}{1}", status.StatusInformation, Environment.NewLine);

						haveReason = true;
					}

					// Note: Because Mono does not include any elements in the chain (at least on macOS), we need
					// to find the inner-most exception and append its Message.
					if (!haveReason) {
						var innerException = ex;

						while (innerException.InnerException != null)
							innerException = innerException.InnerException;

						message.AppendLine ("\u2022 " + innerException.Message);
					}
				}
			} else if (!starttls && standardPorts.Contains (port)) {
				string an = "AEHIOS".IndexOf (protocol[0]) != -1 ? "an" : "a";

				message.AppendFormat (CultureInfo.InvariantCulture, "When connecting to {0} {1} service, port {2} is typically reserved for plain-text connections. If{3}", an, protocol, port, Environment.NewLine);
				message.AppendFormat (CultureInfo.InvariantCulture, "you intended to connect to {0} on the SSL port, try connecting to port {1} instead. Otherwise,{2}", protocol, sslPort, Environment.NewLine);
				message.AppendLine ("if you intended to use STARTTLS, make sure to use the following code:");
				message.AppendLine ();
				message.AppendFormat ("client.Connect (\"{0}\", {1}, SecureSocketOptions.StartTls);{2}", host, port, Environment.NewLine);
			} else {
				message.AppendLine ("This usually means that the SSL certificate presented by the server is not trusted by the system for one or more of");
				message.AppendLine ("the following reasons:");
				message.AppendLine ();
				message.AppendLine ("1. The server is using a self-signed certificate which cannot be verified.");
				message.AppendLine ("2. The local system is missing a Root or Intermediate certificate needed to verify the server's certificate.");
				message.AppendLine ("3. A Certificate Authority CRL server for one or more of the certificates in the chain is temporarily unavailable.");
				message.AppendLine ("4. The certificate presented by the server is expired or invalid.");
				message.AppendLine ("5. The set of SSL/TLS protocols supported by the client and server do not match.");
				if (!starttls)
					message.AppendLine ("6. You are trying to connect to a port which does not support SSL/TLS.");
				message.AppendLine ();
				message.AppendLine ("See " + SslHandshakeHelpLink + " for possible solutions.");
			}

			return new SslHandshakeException (message.ToString (), ex) { ServerCertificate = certificate, RootCertificateAuthority = root };
		}
	}

	class SslChainElement
	{
		public readonly X509Certificate Certificate;
		public readonly X509ChainStatus[] ChainElementStatus;
		public readonly string Information;

		public SslChainElement (X509ChainElement element)
		{
			Certificate = new X509Certificate2 (element.Certificate.RawData);
			ChainElementStatus = element.ChainElementStatus;
			Information = element.Information;
		}
	}

	class SslCertificateValidationInfo
	{
		public readonly List<SslChainElement> ChainElements;
		public readonly X509ChainStatus[] ChainStatus;
		public readonly SslPolicyErrors SslPolicyErrors;
		public readonly X509Certificate Certificate;
		public readonly string Host;

		public SslCertificateValidationInfo (object sender, X509Certificate certificate, X509Chain chain, SslPolicyErrors sslPolicyErrors)
		{
			Certificate = new X509Certificate2 (certificate.Export (X509ContentType.Cert));
			ChainElements = new List<SslChainElement> ();
			SslPolicyErrors = sslPolicyErrors;
			ChainStatus = chain.ChainStatus;
			Host = sender as string;

			// Note: we need to copy the ChainElements because the chain will be destroyed
			foreach (var element in chain.ChainElements)
				ChainElements.Add (new SslChainElement (element));
		}
	}
}
