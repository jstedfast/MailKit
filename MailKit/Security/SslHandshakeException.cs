//
// SslHandshakeException.cs
//
// Author: Jeffrey Stedfast <jestedfa@microsoft.com>
//
// Copyright (c) 2013-2018 Xamarin Inc. (www.xamarin.com)
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
#if SERIALIZABLE
using System.Runtime.Serialization;
#endif

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
		const string SslHandshakeHelpLink = "https://github.com/jstedfast/MailKit/blob/master/FAQ.md#InvalidSslCertificate";
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
		}
#endif

		static string AddHelpLinkSuggestion (string message)
		{
			return string.Format ("{0}{1}See {2} for possible solutions to this problem.",
			                      message, Environment.NewLine, SslHandshakeHelpLink);
		}

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

		internal static SslHandshakeException Create (Exception ex, bool starttls)
		{
			var message = DefaultMessage + Environment.NewLine + Environment.NewLine;
			var aggregate = ex as AggregateException;

			if (aggregate != null) {
				aggregate = aggregate.Flatten ();

				if (aggregate.InnerExceptions.Count == 1)
					ex = aggregate.InnerExceptions[0];
				else
					ex = aggregate;
			}

			if (starttls) {
				message += "The SSL certificate presented by the server is not trusted by the system for one or more of the following reasons:";
			} else {
				message += "One possibility is that you are trying to connect to a port which does not support SSL/TLS." + Environment.NewLine;
				message += Environment.NewLine;
				message += "The other possibility is that the SSL certificate presented by the server is not trusted by the system for one or more of the following reasons:";
			}

			message += Environment.NewLine;
			message += "1. The server is using a self-signed certificate which cannot be verified." + Environment.NewLine;
			message += "2. The local system is missing a Root or Intermediate certificate needed to verify the server's certificate." + Environment.NewLine;
			message += "3. The certificate presented by the server is expired or invalid." + Environment.NewLine;
			message += Environment.NewLine;
			message += "See " + SslHandshakeHelpLink + " for possible solutions.";

			return new SslHandshakeException (message, ex);
		}
	}
}
