//
// SendResult.cs
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
using System.Collections.Generic;

using MimeKit;

namespace MailKit {
	/// <summary>
	/// The result of sending a message.
	/// </summary>
	/// <remarks>
	/// The result of sending a message.
	/// </remarks>
	public class SendResult
	{
		/// <summary>
		/// Initializes a new instance of the <see cref="SendResult"/> class.
		/// </summary>
		/// <remarks>
		/// Creates a new <see cref="SendResult"/>.
		/// </remarks>
		/// <param name="responseText">The final free-form text response from the server.</param>
		/// <param name="acceptedRecipients">The recipients that were accepted by the server.</param>
		/// <param name="rejectedRecipients">The recipients that were rejected by the server.</param>
		/// <exception cref="ArgumentNullException">
		/// <para><paramref name="responseText"/> is <see langword="null" />.</para>
		/// <para>-or-</para>
		/// <para><paramref name="acceptedRecipients"/> is <see langword="null" />.</para>
		/// <para>-or-</para>
		/// <para><paramref name="rejectedRecipients"/> is <see langword="null" />.</para>
		/// </exception>
		public SendResult (string responseText, IReadOnlyList<MailboxAddress> acceptedRecipients, IReadOnlyList<MailboxAddress> rejectedRecipients)
		{
			if (responseText == null)
				throw new ArgumentNullException (nameof (responseText));

			if (acceptedRecipients == null)
				throw new ArgumentNullException (nameof (acceptedRecipients));

			if (rejectedRecipients == null)
				throw new ArgumentNullException (nameof (rejectedRecipients));

			ResponseText = responseText;
			AcceptedRecipients = acceptedRecipients;
			RejectedRecipients = rejectedRecipients;
		}

		/// <summary>
		/// Get the final free-form text response from the server.
		/// </summary>
		/// <remarks>
		/// Gets the final free-form text response from the server.
		/// </remarks>
		/// <value>The response text.</value>
		public string ResponseText {
			get;
		}

		/// <summary>
		/// Get the recipients that were accepted by the server.
		/// </summary>
		/// <remarks>
		/// Gets the recipients that were accepted by the server.
		/// </remarks>
		/// <value>The accepted recipients.</value>
		public IReadOnlyList<MailboxAddress> AcceptedRecipients {
			get;
		}

		/// <summary>
		/// Get the recipients that were rejected by the server.
		/// </summary>
		/// <remarks>
		/// <para>Gets the recipients that were rejected by the server.</para>
		/// <para>By default, the <see cref="MailKit.Net.Smtp.SmtpClient"/> throws an exception when a
		/// recipient is rejected, so this list will only contain recipients if
		/// <see cref="MailKit.Net.Smtp.ISmtpSendRequest.OnRecipientNotAccepted(MailboxAddress, MailKit.Net.Smtp.SmtpResponse)"/>
		/// has been overridden to not throw.</para>
		/// </remarks>
		/// <value>The rejected recipients.</value>
		public IReadOnlyList<MailboxAddress> RejectedRecipients {
			get;
		}
	}
}
