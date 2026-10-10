//
// SmtpSendRequest.cs
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
	/// A request for sending a message via SMTP.
	/// </summary>
	/// <remarks>
	/// <para>A request for sending a message via SMTP.</para>
	/// <para>In addition to the options provided by <see cref="SendRequest"/>, an SMTP send request
	/// can specify Delivery Status Notification options and can be notified as the SMTP server accepts
	/// or rejects the sender and each of the recipients.</para>
	/// </remarks>
	/// <example>
	/// <code language="c#" source="Examples\SmtpExamples.cs" region="DeliveryStatusNotification"/>
	/// </example>
	public class SmtpSendRequest : SendRequest, ISmtpSendRequest
	{
		/// <summary>
		/// Initializes a new instance of the <see cref="SmtpSendRequest"/> class.
		/// </summary>
		/// <remarks>
		/// <para>Creates a new <see cref="SmtpSendRequest"/>.</para>
		/// <para>The envelope sender and recipients will be determined from the message headers.</para>
		/// </remarks>
		/// <param name="message">The message.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <paramref name="message"/> is <see langword="null" />.
		/// </exception>
		public SmtpSendRequest (MimeMessage message) : base (message)
		{
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="SmtpSendRequest"/> class.
		/// </summary>
		/// <remarks>
		/// Creates a new <see cref="SmtpSendRequest"/> using the supplied envelope sender and recipients.
		/// </remarks>
		/// <param name="message">The message.</param>
		/// <param name="sender">The mailbox address to use as the envelope sender.</param>
		/// <param name="recipients">The mailbox addresses to use as the envelope recipients.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <para><paramref name="message"/> is <see langword="null" />.</para>
		/// <para>-or-</para>
		/// <para><paramref name="sender"/> is <see langword="null" />.</para>
		/// <para>-or-</para>
		/// <para><paramref name="recipients"/> is <see langword="null" />.</para>
		/// </exception>
		public SmtpSendRequest (MimeMessage message, MailboxAddress sender, IEnumerable<MailboxAddress> recipients) : base (message, sender, recipients)
		{
		}

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
		public string? EnvelopeId {
			get; set;
		}

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
		public DeliveryStatusNotificationType DeliveryStatusNotificationType {
			get; set;
		}

		/// <summary>
		/// Get or set the types of delivery status notification desired for each of the recipients.
		/// </summary>
		/// <remarks>
		/// <para>Gets or sets the types of delivery status notification desired for each of the
		/// recipients.</para>
		/// <para>This value is returned by the default implementation of
		/// <see cref="GetDeliveryStatusNotifications(MailboxAddress)"/>.</para>
		/// </remarks>
		/// <value>The desired delivery status notification type or <see langword="null" /> to use the
		/// server's default.</value>
		public DeliveryStatusNotification? DeliveryStatusNotifications {
			get; set;
		}

		/// <summary>
		/// Get the types of delivery status notification desired for the specified recipient mailbox.
		/// </summary>
		/// <remarks>
		/// <para>Gets the types of delivery status notification desired for the specified recipient
		/// mailbox.</para>
		/// <para>The default implementation returns <see cref="DeliveryStatusNotifications"/>.</para>
		/// </remarks>
		/// <returns>The desired delivery status notification type or <see langword="null" /> to use the
		/// server's default.</returns>
		/// <param name="recipient">The recipient mailbox.</param>
		public virtual DeliveryStatusNotification? GetDeliveryStatusNotifications (MailboxAddress recipient)
		{
			return DeliveryStatusNotifications;
		}

		/// <summary>
		/// Called when the sender is accepted by the SMTP server.
		/// </summary>
		/// <remarks>
		/// <para>Called when the sender is accepted by the SMTP server.</para>
		/// <para>The default implementation does nothing.</para>
		/// </remarks>
		/// <param name="sender">The mailbox used in the <c>MAIL FROM</c> command.</param>
		/// <param name="response">The response to the <c>MAIL FROM</c> command.</param>
		public virtual void OnSenderAccepted (MailboxAddress sender, SmtpResponse response)
		{
		}

		/// <summary>
		/// Called when the sender is not accepted by the SMTP server.
		/// </summary>
		/// <remarks>
		/// <para>Called when the sender is not accepted by the SMTP server.</para>
		/// <para>The default implementation throws an appropriate <see cref="SmtpCommandException"/>.</para>
		/// </remarks>
		/// <param name="sender">The mailbox used in the <c>MAIL FROM</c> command.</param>
		/// <param name="response">The response to the <c>MAIL FROM</c> command.</param>
		/// <exception cref="SmtpCommandException">
		/// The sender was not accepted.
		/// </exception>
		public virtual void OnSenderNotAccepted (MailboxAddress sender, SmtpResponse response)
		{
			throw new SmtpCommandException (SmtpErrorCode.SenderNotAccepted, SmtpCommand.MailFrom, response, sender);
		}

		/// <summary>
		/// Called when a recipient is accepted by the SMTP server.
		/// </summary>
		/// <remarks>
		/// <para>Called when a recipient is accepted by the SMTP server.</para>
		/// <para>The default implementation does nothing.</para>
		/// </remarks>
		/// <param name="recipient">The mailbox used in the <c>RCPT TO</c> command.</param>
		/// <param name="response">The response to the <c>RCPT TO</c> command.</param>
		public virtual void OnRecipientAccepted (MailboxAddress recipient, SmtpResponse response)
		{
		}

		/// <summary>
		/// Called when a recipient is not accepted by the SMTP server.
		/// </summary>
		/// <remarks>
		/// <para>Called when a recipient is not accepted by the SMTP server.</para>
		/// <para>The default implementation throws an appropriate <see cref="SmtpCommandException"/>.
		/// Override this method to not throw in order to send the message to the remaining recipients.</para>
		/// </remarks>
		/// <param name="recipient">The mailbox used in the <c>RCPT TO</c> command.</param>
		/// <param name="response">The response to the <c>RCPT TO</c> command.</param>
		/// <exception cref="SmtpCommandException">
		/// The recipient was not accepted.
		/// </exception>
		public virtual void OnRecipientNotAccepted (MailboxAddress recipient, SmtpResponse response)
		{
			throw new SmtpCommandException (SmtpErrorCode.RecipientNotAccepted, SmtpCommand.RcptTo, response, recipient);
		}

		/// <summary>
		/// Called when none of the recipients were accepted by the SMTP server.
		/// </summary>
		/// <remarks>
		/// <para>Called when none of the recipients were accepted by the SMTP server.</para>
		/// <para>This method is only called if <see cref="OnRecipientNotAccepted(MailboxAddress, SmtpResponse)"/>
		/// does not throw. If this method does not throw an exception, an <see cref="SmtpCommandException"/>
		/// will be thrown.</para>
		/// <para>The default implementation does nothing.</para>
		/// </remarks>
		public virtual void OnNoRecipientsAccepted ()
		{
		}
	}
}
