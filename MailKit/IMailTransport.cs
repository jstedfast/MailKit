//
// IMailTransport.cs
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
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;

using MimeKit;

namespace MailKit {
	/// <summary>
	/// An interface for sending messages.
	/// </summary>
	/// <remarks>
	/// An interface for sending messages.
	/// </remarks>
	public interface IMailTransport : IMailService
	{
		/// <summary>
		/// Send the specified message.
		/// </summary>
		/// <remarks>
		/// <para>Sends the specified message.</para>
		/// <para>The sender address is determined by checking the following
		/// message headers (in order of precedence): Resent-Sender,
		/// Resent-From, Sender, and From.</para>
		/// <para>If either the Resent-Sender or Resent-From addresses are present,
		/// the recipients are collected from the Resent-To, Resent-Cc, and
		/// Resent-Bcc headers, otherwise the To, Cc, and Bcc headers are used.</para>
		/// </remarks>
		/// <param name="message">The message.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <param name="progress">The progress reporting mechanism.</param>
		void Send (MimeMessage message, CancellationToken cancellationToken = default (CancellationToken), ITransferProgress progress = null);

		/// <summary>
		/// Asynchronously send the specified message.
		/// </summary>
		/// <remarks>
		/// <para>Asynchronously sends the specified message.</para>
		/// <para>The sender address is determined by checking the following
		/// message headers (in order of precedence): Resent-Sender,
		/// Resent-From, Sender, and From.</para>
		/// <para>If either the Resent-Sender or Resent-From addresses are present,
		/// the recipients are collected from the Resent-To, Resent-Cc, and
		/// Resent-Bcc headers, otherwise the To, Cc, and Bcc headers are used.</para>
		/// </remarks>
		/// <returns>An asynchronous task context.</returns>
		/// <param name="message">The message.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <param name="progress">The progress reporting mechanism.</param>
		Task SendAsync (MimeMessage message, CancellationToken cancellationToken = default (CancellationToken), ITransferProgress progress = null);

		/// <summary>
		/// Send the specified message using the supplied sender and recipients.
		/// </summary>
		/// <remarks>
		/// Sends the specified message using the supplied sender and recipients.
		/// </remarks>
		/// <param name="message">The message.</param>
		/// <param name="sender">The mailbox address to use for sending the message.</param>
		/// <param name="recipients">The mailbox addresses that should receive the message.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <param name="progress">The progress reporting mechanism.</param>
		void Send (MimeMessage message, MailboxAddress sender, IEnumerable<MailboxAddress> recipients, CancellationToken cancellationToken = default (CancellationToken), ITransferProgress progress = null);

		/// <summary>
		/// Asynchronously send the specified message using the supplied sender and recipients.
		/// </summary>
		/// <remarks>
		/// Asynchronously sends the specified message using the supplied sender and recipients.
		/// </remarks>
		/// <returns>An asynchronous task context.</returns>
		/// <param name="message">The message.</param>
		/// <param name="sender">The mailbox address to use for sending the message.</param>
		/// <param name="recipients">The mailbox addresses that should receive the message.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <param name="progress">The progress reporting mechanism.</param>
		Task SendAsync (MimeMessage message, MailboxAddress sender, IEnumerable<MailboxAddress> recipients, CancellationToken cancellationToken = default (CancellationToken), ITransferProgress progress = null);

		/// <summary>
		/// Send the specified message.
		/// </summary>
		/// <remarks>
		/// <para>Sends the specified message.</para>
		/// <para>The sender address is determined by checking the following
		/// message headers (in order of precedence): Resent-Sender,
		/// Resent-From, Sender, and From.</para>
		/// <para>If either the Resent-Sender or Resent-From addresses are present,
		/// the recipients are collected from the Resent-To, Resent-Cc, and
		/// Resent-Bcc headers, otherwise the To, Cc, and Bcc headers are used.</para>
		/// </remarks>
		/// <param name="options">The formatting options.</param>
		/// <param name="message">The message.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <param name="progress">The progress reporting mechanism.</param>
		void Send (FormatOptions options, MimeMessage message, CancellationToken cancellationToken = default (CancellationToken), ITransferProgress progress = null);

		/// <summary>
		/// Asynchronously send the specified message.
		/// </summary>
		/// <remarks>
		/// <para>Asynchronously sends the specified message.</para>
		/// <para>The sender address is determined by checking the following
		/// message headers (in order of precedence): Resent-Sender,
		/// Resent-From, Sender, and From.</para>
		/// <para>If either the Resent-Sender or Resent-From addresses are present,
		/// the recipients are collected from the Resent-To, Resent-Cc, and
		/// Resent-Bcc headers, otherwise the To, Cc, and Bcc headers are used.</para>
		/// </remarks>
		/// <returns>An asynchronous task context.</returns>
		/// <param name="options">The formatting options.</param>
		/// <param name="message">The message.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <param name="progress">The progress reporting mechanism.</param>
		Task SendAsync (FormatOptions options, MimeMessage message, CancellationToken cancellationToken = default (CancellationToken), ITransferProgress progress = null);

		/// <summary>
		/// Send the specified message using the supplied sender and recipients.
		/// </summary>
		/// <remarks>
		/// Sends the specified message using the supplied sender and recipients.
		/// </remarks>
		/// <param name="options">The formatting options.</param>
		/// <param name="message">The message.</param>
		/// <param name="sender">The mailbox address to use for sending the message.</param>
		/// <param name="recipients">The mailbox addresses that should receive the message.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <param name="progress">The progress reporting mechanism.</param>
		void Send (FormatOptions options, MimeMessage message, MailboxAddress sender, IEnumerable<MailboxAddress> recipients, CancellationToken cancellationToken = default (CancellationToken), ITransferProgress progress = null);

		/// <summary>
		/// Asynchronously send the specified message using the supplied sender and recipients.
		/// </summary>
		/// <remarks>
		/// Asynchronously sends the specified message using the supplied sender and recipients.
		/// </remarks>
		/// <returns>An asynchronous task context.</returns>
		/// <param name="options">The formatting options.</param>
		/// <param name="message">The message.</param>
		/// <param name="sender">The mailbox address to use for sending the message.</param>
		/// <param name="recipients">The mailbox addresses that should receive the message.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <param name="progress">The progress reporting mechanism.</param>
		Task SendAsync (FormatOptions options, MimeMessage message, MailboxAddress sender, IEnumerable<MailboxAddress> recipients, CancellationToken cancellationToken = default (CancellationToken), ITransferProgress progress = null);

		/// <summary>
		/// Occurs when a message is successfully sent via the transport.
		/// </summary>
		/// <remarks>
		/// The <see cref="MessageSent"/> event will be emitted each time a message is successfully sent.
		/// </remarks>
		event EventHandler<MessageSentEventArgs> MessageSent;
	}
}
