//
// SendRequest.cs
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
	/// A request for sending a message.
	/// </summary>
	/// <remarks>
	/// A request for sending a message.
	/// </remarks>
	public class SendRequest : ISendRequest
	{
		/// <summary>
		/// Initializes a new instance of the <see cref="SendRequest"/> class.
		/// </summary>
		/// <remarks>
		/// <para>Creates a new <see cref="SendRequest"/>.</para>
		/// <para>The envelope sender and recipients will be determined from the message headers.</para>
		/// </remarks>
		/// <param name="message">The message.</param>
		/// <exception cref="ArgumentNullException">
		/// <paramref name="message"/> is <see langword="null" />.
		/// </exception>
		public SendRequest (MimeMessage message)
		{
			if (message == null)
				throw new ArgumentNullException (nameof (message));

			Message = message;
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="SendRequest"/> class.
		/// </summary>
		/// <remarks>
		/// Creates a new <see cref="SendRequest"/> using the supplied envelope sender and recipients.
		/// </remarks>
		/// <param name="message">The message.</param>
		/// <param name="sender">The mailbox address to use as the envelope sender.</param>
		/// <param name="recipients">The mailbox addresses to use as the envelope recipients.</param>
		/// <exception cref="ArgumentNullException">
		/// <para><paramref name="message"/> is <see langword="null" />.</para>
		/// <para>-or-</para>
		/// <para><paramref name="sender"/> is <see langword="null" />.</para>
		/// <para>-or-</para>
		/// <para><paramref name="recipients"/> is <see langword="null" />.</para>
		/// </exception>
		public SendRequest (MimeMessage message, MailboxAddress sender, IEnumerable<MailboxAddress> recipients) : this (message)
		{
			if (sender == null)
				throw new ArgumentNullException (nameof (sender));

			if (recipients == null)
				throw new ArgumentNullException (nameof (recipients));

			Sender = sender;
			Recipients = new List<MailboxAddress> (recipients);
		}

		/// <summary>
		/// Get the message that should be sent.
		/// </summary>
		/// <remarks>
		/// Gets the message that should be sent.
		/// </remarks>
		/// <value>The message.</value>
		public MimeMessage Message {
			get;
		}

		/// <summary>
		/// Get or set the mailbox address to use as the envelope sender.
		/// </summary>
		/// <remarks>
		/// <para>Gets or sets the mailbox address to use as the envelope sender.</para>
		/// <para>If <see langword="null" />, the sender address is determined by checking the following
		/// message headers (in order of precedence): Resent-Sender, Resent-From, Sender, and From.</para>
		/// </remarks>
		/// <value>The envelope sender or <see langword="null" /> if it should be determined from the message
		/// headers.</value>
		public MailboxAddress? Sender {
			get; set;
		}

		/// <summary>
		/// Get or set the mailbox addresses to use as the envelope recipients.
		/// </summary>
		/// <remarks>
		/// <para>Gets or sets the mailbox addresses to use as the envelope recipients.</para>
		/// <para>If <see langword="null" /> and either the Resent-Sender or Resent-From addresses are
		/// present, the recipients are collected from the Resent-To, Resent-Cc, and Resent-Bcc headers,
		/// otherwise the To, Cc, and Bcc headers are used.</para>
		/// </remarks>
		/// <value>The envelope recipients or <see langword="null" /> if they should be determined from the
		/// message headers.</value>
		public IList<MailboxAddress>? Recipients {
			get; set;
		}

		/// <summary>
		/// Get or set the transfer progress reporting mechanism.
		/// </summary>
		/// <remarks>
		/// Gets or sets the transfer progress reporting mechanism.
		/// </remarks>
		/// <value>The transfer progress mechanism.</value>
		public ITransferProgress? TransferProgress {
			get; set;
		}

		/// <summary>
		/// Called when the message is about to be sent to the server.
		/// </summary>
		/// <remarks>
		/// <para>Called immediately before the message data is sent to the server.</para>
		/// <para>If this method is called but <see cref="OnCompleted(IMailTransport, SendResult)"/> is not
		/// called and the operation fails with an exception other than a <see cref="CommandException"/>,
		/// then it is unknown whether or not the server accepted the message for delivery.</para>
		/// <para>The default implementation does nothing.</para>
		/// </remarks>
		/// <param name="transport">The transport that is sending the message.</param>
		public virtual void OnStarted (IMailTransport transport)
		{
		}

		/// <summary>
		/// Called when the server has accepted the message for delivery.
		/// </summary>
		/// <remarks>
		/// <para>Called after the server has accepted the message for delivery.</para>
		/// <para>The default implementation does nothing.</para>
		/// </remarks>
		/// <param name="transport">The transport that sent the message.</param>
		/// <param name="result">The result of sending the message.</param>
		public virtual void OnCompleted (IMailTransport transport, SendResult result)
		{
		}
	}
}
