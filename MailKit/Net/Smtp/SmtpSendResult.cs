//
// SmtpSendResult.cs
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

using System.Collections.Generic;

using MimeKit;

namespace MailKit.Net.Smtp {
	/// <summary>
	/// The result of sending a message via SMTP.
	/// </summary>
	/// <remarks>
	/// <para>The result of sending a message via SMTP.</para>
	/// <para>The <see cref="SmtpClient"/> returns an <see cref="SmtpSendResult"/> from its
	/// <a href="Overload_MailKit_Net_Smtp_SmtpClient_Send.htm">Send</a> and
	/// <a href="Overload_MailKit_Net_Smtp_SmtpClient_SendAsync.htm">SendAsync</a> methods.</para>
	/// </remarks>
	public class SmtpSendResult : SendResult
	{
		/// <summary>
		/// Initializes a new instance of the <see cref="SmtpSendResult"/> class.
		/// </summary>
		/// <remarks>
		/// Creates a new <see cref="SmtpSendResult"/>.
		/// </remarks>
		/// <param name="statusCode">The final status code from the server.</param>
		/// <param name="responseText">The final free-form text response from the server.</param>
		/// <param name="acceptedRecipients">The recipients that were accepted by the server.</param>
		/// <param name="rejectedRecipients">The recipients that were rejected by the server.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <para><paramref name="responseText"/> is <see langword="null" />.</para>
		/// <para>-or-</para>
		/// <para><paramref name="acceptedRecipients"/> is <see langword="null" />.</para>
		/// <para>-or-</para>
		/// <para><paramref name="rejectedRecipients"/> is <see langword="null" />.</para>
		/// </exception>
		public SmtpSendResult (SmtpStatusCode statusCode, string responseText, IReadOnlyList<MailboxAddress> acceptedRecipients, IReadOnlyList<MailboxAddress> rejectedRecipients) : base (responseText, acceptedRecipients, rejectedRecipients)
		{
			StatusCode = statusCode;
		}

		/// <summary>
		/// Get the final status code from the server.
		/// </summary>
		/// <remarks>
		/// Gets the final status code from the server.
		/// </remarks>
		/// <value>The status code.</value>
		public SmtpStatusCode StatusCode {
			get;
		}
	}
}