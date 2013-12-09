//
// IMessageService.cs
//
// Author: Jeffrey Stedfast <jeff@xamarin.com>
//
// Copyright (c) 2013 Jeffrey Stedfast
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
using System.Threading;
using System.Collections.Generic;
using System.Security.Cryptography.X509Certificates;

namespace MailKit {
	/// <summary>
	/// An interface for message services such as SMTP, POP3, or IMAP.
	/// </summary>
	/// <remarks>
	/// Implemented by <see cref="MailKit.Net.Smtp.SmtpClient"/>
	/// and <see cref="MailKit.Net.Pop3.Pop3Client"/>.
	/// </remarks>
	public interface IMessageService : IDisposable
	{
		/// <summary>
		/// Gets or sets the client SSL certificates.
		/// </summary>
		/// <remarks>
		/// <para>Some servers may require the client SSL certificates in order
		/// to allow the user to connect.</para>
		/// <para>This property should be set before calling <see cref="Connect"/>.</para>
		/// </remarks>
		/// <value>The client SSL certificates.</value>
		X509CertificateCollection ClientCertificates { get; set; }

		/// <summary>
		/// Gets the authentication mechanisms supported by the message service.
		/// </summary>
		/// <remarks>
		/// The authentication mechanisms are queried durring the
		/// <see cref="Connect"/> method.
		/// </remarks>
		/// <value>The supported authentication mechanisms.</value>
		HashSet<string> AuthenticationMechanisms { get; }

		/// <summary>
		/// Gets whether or not the service is currently connected.
		/// </summary>
		/// <value><c>true</c> if the service connected; otherwise, <c>false</c>.</value>
		bool IsConnected { get; }

		/// <summary>
		/// Establishes a connection to the specified server using the supplied credentials.
		/// </summary>
		/// <remarks>
		/// If a successful connection is made, the <see cref="AuthenticationMechanisms"/>
		/// property will be populated.
		/// </remarks>
		/// <param name="uri">The server URI.</param>
		/// <param name="credentials">The user's credentials.</param>
		/// <param name="token">A cancellation token.</param>
		void Connect (Uri uri, ICredentials credentials, CancellationToken token);

		/// <summary>
		/// Disconnect the service.
		/// </summary>
		/// <remarks>
		/// If <paramref name="quit"/> is <c>true</c>, a "QUIT" command will be issued in order to disconnect cleanly.
		/// </remarks>
		/// <param name="quit">If set to <c>true</c>, a "QUIT" command will be issued in order to disconnect cleanly.</param>
		/// <param name="token">A cancellation token.</param>
		void Disconnect (bool quit, CancellationToken token);
	}
}
