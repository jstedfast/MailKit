//
// ISmtpSendRequest.cs
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

using MimeKit;

namespace MailKit.Net.Smtp {
	/// <summary>
	/// A request for sending a message via SMTP.
	/// </summary>
	/// <remarks>
	/// <para>A request for sending a message via SMTP.</para>
	/// <para>In addition to the options provided by <see cref="ISendRequest"/>, an SMTP send request
	/// can specify Delivery Status Notification options and can be notified as the SMTP server accepts
	/// or rejects the sender and each of the recipients.</para>
	/// </remarks>
	/// <example>
	/// <code language="c#" source="Examples\SmtpExamples.cs" region="DeliveryStatusNotification"/>
	/// </example>
	public interface ISmtpSendRequest : ISendRequest
	{
		/// <summary>
		/// Get or set the envelope identifier to be used with delivery status notifications.
		/// </summary>
		/// <remarks>
		/// <para>The envelope identifier, if non-empty, is useful in determining which message a delivery
		/// status notification was issued for.</para>
		/// <para>The envelope identifier should be unique and may be up to 100 characters in length, but
		/// must consist only of printable ASCII characters and no white space.</para>
		/// <para>This value is only used if the SMTP server supports the <see cref="SmtpCapability.Dsn"/>
		/// extension.</para>
		/// <para>For more information, see
		/// <a href="https://tools.ietf.org/html/rfc3461#section-4.4">rfc3461, section 4.4</a>.</para>
		/// </remarks>
		/// <value>The envelope identifier.</value>
		string? EnvelopeId { get; set; }

		/// <summary>
		/// Get or set how much of the message to include in any failed delivery status notifications.
		/// </summary>
		/// <remarks>
		/// <para>Gets or sets how much of the message to include in any failed delivery status
		/// notifications.</para>
		/// <para>This value is only used if the SMTP server supports the <see cref="SmtpCapability.Dsn"/>
		/// extension.</para>
		/// </remarks>
		/// <value>A value indicating how much of the message to include in a failure delivery status
		/// notification.</value>
		DeliveryStatusNotificationType DeliveryStatusNotificationType { get; set; }

		/// <summary>
		/// Get the types of delivery status notification desired for the specified recipient mailbox.
		/// </summary>
		/// <remarks>
		/// <para>Gets the types of delivery status notification desired for the specified recipient
		/// mailbox.</para>
		/// <para>This method is only called if the SMTP server supports the <see cref="SmtpCapability.Dsn"/>
		/// extension.</para>
		/// </remarks>
		/// <returns>The desired delivery status notification type or <see langword="null" /> to use the
		/// server's default.</returns>
		/// <param name="recipient">The recipient mailbox.</param>
		DeliveryStatusNotification? GetDeliveryStatusNotifications (MailboxAddress recipient);

		/// <summary>
		/// Called when the sender is accepted by the SMTP server.
		/// </summary>
		/// <remarks>
		/// Called when the sender is accepted by the SMTP server.
		/// </remarks>
		/// <param name="sender">The mailbox used in the <c>MAIL FROM</c> command.</param>
		/// <param name="response">The response to the <c>MAIL FROM</c> command.</param>
		void OnSenderAccepted (MailboxAddress sender, SmtpResponse response);

		/// <summary>
		/// Called when the sender is not accepted by the SMTP server.
		/// </summary>
		/// <remarks>
		/// <para>Called when the sender is not accepted by the SMTP server.</para>
		/// <para>Implementations should generally throw an appropriate <see cref="SmtpCommandException"/>.</para>
		/// </remarks>
		/// <param name="sender">The mailbox used in the <c>MAIL FROM</c> command.</param>
		/// <param name="response">The response to the <c>MAIL FROM</c> command.</param>
		void OnSenderNotAccepted (MailboxAddress sender, SmtpResponse response);

		/// <summary>
		/// Called when a recipient is accepted by the SMTP server.
		/// </summary>
		/// <remarks>
		/// Called when a recipient is accepted by the SMTP server.
		/// </remarks>
		/// <param name="recipient">The mailbox used in the <c>RCPT TO</c> command.</param>
		/// <param name="response">The response to the <c>RCPT TO</c> command.</param>
		void OnRecipientAccepted (MailboxAddress recipient, SmtpResponse response);

		/// <summary>
		/// Called when a recipient is not accepted by the SMTP server.
		/// </summary>
		/// <remarks>
		/// <para>Called when a recipient is not accepted by the SMTP server.</para>
		/// <para>If this method throws an exception, the message will not be sent. Otherwise, the
		/// recipient will be added to the <see cref="SendResult.RejectedRecipients"/> and the message
		/// will be sent to the remaining recipients.</para>
		/// </remarks>
		/// <param name="recipient">The mailbox used in the <c>RCPT TO</c> command.</param>
		/// <param name="response">The response to the <c>RCPT TO</c> command.</param>
		void OnRecipientNotAccepted (MailboxAddress recipient, SmtpResponse response);

		/// <summary>
		/// Called when none of the recipients were accepted by the SMTP server.
		/// </summary>
		/// <remarks>
		/// <para>Called when none of the recipients were accepted by the SMTP server.</para>
		/// <para>This method is only called if <see cref="OnRecipientNotAccepted(MailboxAddress, SmtpResponse)"/>
		/// does not throw. If this method does not throw an exception, an <see cref="SmtpCommandException"/>
		/// will be thrown.</para>
		/// </remarks>
		void OnNoRecipientsAccepted ();
	}
}
