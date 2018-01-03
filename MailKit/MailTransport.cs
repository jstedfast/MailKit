//
// MailTransport.cs
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
	/// An abstract mail transport implementation.
	/// </summary>
	/// <remarks>
	/// An abstract mail transport implementation.
	/// </remarks>
	public abstract class MailTransport : MailService, IMailTransport
	{
		/// <summary>
		/// Initializes a new instance of the <see cref="MailKit.MailTransport"/> class.
		/// </summary>
		/// <remarks>
		/// Initializes a new instance of the <see cref="MailKit.MailTransport"/> class.
		/// </remarks>
		/// <param name="protocolLogger">The protocol logger.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <paramref name="protocolLogger"/> is <c>null</c>.
		/// </exception>
		protected MailTransport (IProtocolLogger protocolLogger) : base (protocolLogger)
		{
		}

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
		/// <example>
		/// <code language="c#" source="Examples\SmtpExamples.cs" region="SendMessage"/>
		/// </example>
		/// <param name="message">The message.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <param name="progress">The progress reporting mechanism.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <paramref name="message"/> is <c>null</c>.
		/// </exception>
		/// <exception cref="System.ObjectDisposedException">
		/// The <see cref="MailTransport"/> has been disposed.
		/// </exception>
		/// <exception cref="ServiceNotConnectedException">
		/// The <see cref="MailTransport"/> is not connected.
		/// </exception>
		/// <exception cref="ServiceNotAuthenticatedException">
		/// Authentication is required before sending a message.
		/// </exception>
		/// <exception cref="System.InvalidOperationException">
		/// <para>A sender has not been specified.</para>
		/// <para>-or-</para>
		/// <para>No recipients have been specified.</para>
		/// </exception>
		/// <exception cref="System.OperationCanceledException">
		/// The operation has been canceled.
		/// </exception>
		/// <exception cref="System.IO.IOException">
		/// An I/O error occurred.
		/// </exception>
		/// <exception cref="CommandException">
		/// The send command failed.
		/// </exception>
		/// <exception cref="ProtocolException">
		/// A protocol exception occurred.
		/// </exception>
		public virtual void Send (MimeMessage message, CancellationToken cancellationToken = default (CancellationToken), ITransferProgress progress = null)
		{
			Send (FormatOptions.Default, message, cancellationToken, progress);
		}

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
		/// <exception cref="System.ArgumentNullException">
		/// <paramref name="message"/> is <c>null</c>.
		/// </exception>
		/// <exception cref="System.ObjectDisposedException">
		/// The <see cref="MailTransport"/> has been disposed.
		/// </exception>
		/// <exception cref="ServiceNotConnectedException">
		/// The <see cref="MailTransport"/> is not connected.
		/// </exception>
		/// <exception cref="ServiceNotAuthenticatedException">
		/// Authentication is required before sending a message.
		/// </exception>
		/// <exception cref="System.InvalidOperationException">
		/// <para>A sender has not been specified.</para>
		/// <para>-or-</para>
		/// <para>No recipients have been specified.</para>
		/// </exception>
		/// <exception cref="System.OperationCanceledException">
		/// The operation has been canceled.
		/// </exception>
		/// <exception cref="System.IO.IOException">
		/// An I/O error occurred.
		/// </exception>
		/// <exception cref="CommandException">
		/// The send command failed.
		/// </exception>
		/// <exception cref="ProtocolException">
		/// A protocol exception occurred.
		/// </exception>
		public virtual Task SendAsync (MimeMessage message, CancellationToken cancellationToken = default (CancellationToken), ITransferProgress progress = null)
		{
			return SendAsync (FormatOptions.Default, message, cancellationToken, progress);
		}

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
		/// <exception cref="System.ArgumentNullException">
		/// <para><paramref name="message"/> is <c>null</c>.</para>
		/// <para>-or-</para>
		/// <para><paramref name="sender"/> is <c>null</c>.</para>
		/// <para>-or-</para>
		/// <para><paramref name="recipients"/> is <c>null</c>.</para>
		/// </exception>
		/// <exception cref="System.ObjectDisposedException">
		/// The <see cref="MailTransport"/> has been disposed.
		/// </exception>
		/// <exception cref="ServiceNotConnectedException">
		/// The <see cref="MailTransport"/> is not connected.
		/// </exception>
		/// <exception cref="ServiceNotAuthenticatedException">
		/// Authentication is required before sending a message.
		/// </exception>
		/// <exception cref="System.InvalidOperationException">
		/// <para>A sender has not been specified.</para>
		/// <para>-or-</para>
		/// <para>No recipients have been specified.</para>
		/// </exception>
		/// <exception cref="System.OperationCanceledException">
		/// The operation has been canceled.
		/// </exception>
		/// <exception cref="System.IO.IOException">
		/// An I/O error occurred.
		/// </exception>
		/// <exception cref="CommandException">
		/// The send command failed.
		/// </exception>
		/// <exception cref="ProtocolException">
		/// A protocol exception occurred.
		/// </exception>
		public virtual void Send (MimeMessage message, MailboxAddress sender, IEnumerable<MailboxAddress> recipients, CancellationToken cancellationToken = default (CancellationToken), ITransferProgress progress = null)
		{
			Send (FormatOptions.Default, message, sender, recipients, cancellationToken, progress);
		}

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
		/// <exception cref="System.ArgumentNullException">
		/// <para><paramref name="message"/> is <c>null</c>.</para>
		/// <para>-or-</para>
		/// <para><paramref name="sender"/> is <c>null</c>.</para>
		/// <para>-or-</para>
		/// <para><paramref name="recipients"/> is <c>null</c>.</para>
		/// </exception>
		/// <exception cref="System.ObjectDisposedException">
		/// The <see cref="MailTransport"/> has been disposed.
		/// </exception>
		/// <exception cref="ServiceNotConnectedException">
		/// The <see cref="MailTransport"/> is not connected.
		/// </exception>
		/// <exception cref="ServiceNotAuthenticatedException">
		/// Authentication is required before sending a message.
		/// </exception>
		/// <exception cref="System.InvalidOperationException">
		/// <para>A sender has not been specified.</para>
		/// <para>-or-</para>
		/// <para>No recipients have been specified.</para>
		/// </exception>
		/// <exception cref="System.OperationCanceledException">
		/// The operation has been canceled.
		/// </exception>
		/// <exception cref="System.IO.IOException">
		/// An I/O error occurred.
		/// </exception>
		/// <exception cref="CommandException">
		/// The send command failed.
		/// </exception>
		/// <exception cref="ProtocolException">
		/// A protocol exception occurred.
		/// </exception>
		public virtual Task SendAsync (MimeMessage message, MailboxAddress sender, IEnumerable<MailboxAddress> recipients, CancellationToken cancellationToken = default (CancellationToken), ITransferProgress progress = null)
		{
			return SendAsync (FormatOptions.Default, message, sender, recipients, cancellationToken, progress);
		}

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
		/// <example>
		/// <code language="c#" source="Examples\SmtpExamples.cs" region="SendMessageWithOptions"/>
		/// </example>
		/// <param name="options">The formatting options.</param>
		/// <param name="message">The message.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <param name="progress">The progress reporting mechanism.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <para><paramref name="options"/> is <c>null</c>.</para>
		/// <para>-or-</para>
		/// <para><paramref name="message"/> is <c>null</c>.</para>
		/// </exception>
		/// <exception cref="System.ObjectDisposedException">
		/// The <see cref="MailTransport"/> has been disposed.
		/// </exception>
		/// <exception cref="ServiceNotConnectedException">
		/// The <see cref="MailTransport"/> is not connected.
		/// </exception>
		/// <exception cref="ServiceNotAuthenticatedException">
		/// Authentication is required before sending a message.
		/// </exception>
		/// <exception cref="System.InvalidOperationException">
		/// <para>A sender has not been specified.</para>
		/// <para>-or-</para>
		/// <para>No recipients have been specified.</para>
		/// </exception>
		/// <exception cref="System.OperationCanceledException">
		/// The operation has been canceled.
		/// </exception>
		/// <exception cref="System.NotSupportedException">
		/// <para>Internationalized formatting was requested but is not supported by the transport.</para>
		/// </exception>
		/// <exception cref="System.IO.IOException">
		/// An I/O error occurred.
		/// </exception>
		/// <exception cref="CommandException">
		/// The send command failed.
		/// </exception>
		/// <exception cref="ProtocolException">
		/// A protocol exception occurred.
		/// </exception>
		public abstract void Send (FormatOptions options, MimeMessage message, CancellationToken cancellationToken = default (CancellationToken), ITransferProgress progress = null);

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
		/// <exception cref="System.ArgumentNullException">
		/// <para><paramref name="options"/> is <c>null</c>.</para>
		/// <para>-or-</para>
		/// <para><paramref name="message"/> is <c>null</c>.</para>
		/// </exception>
		/// <exception cref="System.ObjectDisposedException">
		/// The <see cref="MailTransport"/> has been disposed.
		/// </exception>
		/// <exception cref="ServiceNotConnectedException">
		/// The <see cref="MailTransport"/> is not connected.
		/// </exception>
		/// <exception cref="ServiceNotAuthenticatedException">
		/// Authentication is required before sending a message.
		/// </exception>
		/// <exception cref="System.InvalidOperationException">
		/// <para>A sender has not been specified.</para>
		/// <para>-or-</para>
		/// <para>No recipients have been specified.</para>
		/// </exception>
		/// <exception cref="System.OperationCanceledException">
		/// The operation has been canceled.
		/// </exception>
		/// <exception cref="System.NotSupportedException">
		/// <para>Internationalized formatting was requested but is not supported by the transport.</para>
		/// </exception>
		/// <exception cref="System.IO.IOException">
		/// An I/O error occurred.
		/// </exception>
		/// <exception cref="CommandException">
		/// The send command failed.
		/// </exception>
		/// <exception cref="ProtocolException">
		/// A protocol exception occurred.
		/// </exception>
		public abstract Task SendAsync (FormatOptions options, MimeMessage message, CancellationToken cancellationToken = default (CancellationToken), ITransferProgress progress = null);

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
		/// <exception cref="System.ArgumentNullException">
		/// <para><paramref name="options"/> is <c>null</c>.</para>
		/// <para>-or-</para>
		/// <para><paramref name="message"/> is <c>null</c>.</para>
		/// <para>-or-</para>
		/// <para><paramref name="sender"/> is <c>null</c>.</para>
		/// <para>-or-</para>
		/// <para><paramref name="recipients"/> is <c>null</c>.</para>
		/// </exception>
		/// <exception cref="System.ObjectDisposedException">
		/// The <see cref="MailTransport"/> has been disposed.
		/// </exception>
		/// <exception cref="ServiceNotConnectedException">
		/// The <see cref="MailTransport"/> is not connected.
		/// </exception>
		/// <exception cref="ServiceNotAuthenticatedException">
		/// Authentication is required before sending a message.
		/// </exception>
		/// <exception cref="System.InvalidOperationException">
		/// <para>A sender has not been specified.</para>
		/// <para>-or-</para>
		/// <para>No recipients have been specified.</para>
		/// </exception>
		/// <exception cref="System.OperationCanceledException">
		/// The operation has been canceled.
		/// </exception>
		/// <exception cref="System.NotSupportedException">
		/// <para>Internationalized formatting was requested but is not supported by the transport.</para>
		/// </exception>
		/// <exception cref="System.IO.IOException">
		/// An I/O error occurred.
		/// </exception>
		/// <exception cref="CommandException">
		/// The send command failed.
		/// </exception>
		/// <exception cref="ProtocolException">
		/// A protocol exception occurred.
		/// </exception>
		public abstract void Send (FormatOptions options, MimeMessage message, MailboxAddress sender, IEnumerable<MailboxAddress> recipients, CancellationToken cancellationToken = default (CancellationToken), ITransferProgress progress = null);

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
		/// <exception cref="System.ArgumentNullException">
		/// <para><paramref name="options"/> is <c>null</c>.</para>
		/// <para>-or-</para>
		/// <para><paramref name="message"/> is <c>null</c>.</para>
		/// <para>-or-</para>
		/// <para><paramref name="sender"/> is <c>null</c>.</para>
		/// <para>-or-</para>
		/// <para><paramref name="recipients"/> is <c>null</c>.</para>
		/// </exception>
		/// <exception cref="System.ObjectDisposedException">
		/// The <see cref="MailTransport"/> has been disposed.
		/// </exception>
		/// <exception cref="ServiceNotConnectedException">
		/// The <see cref="MailTransport"/> is not connected.
		/// </exception>
		/// <exception cref="ServiceNotAuthenticatedException">
		/// Authentication is required before sending a message.
		/// </exception>
		/// <exception cref="System.InvalidOperationException">
		/// <para>A sender has not been specified.</para>
		/// <para>-or-</para>
		/// <para>No recipients have been specified.</para>
		/// </exception>
		/// <exception cref="System.OperationCanceledException">
		/// The operation has been canceled.
		/// </exception>
		/// <exception cref="System.NotSupportedException">
		/// <para>Internationalized formatting was requested but is not supported by the transport.</para>
		/// </exception>
		/// <exception cref="System.IO.IOException">
		/// An I/O error occurred.
		/// </exception>
		/// <exception cref="CommandException">
		/// The send command failed.
		/// </exception>
		/// <exception cref="ProtocolException">
		/// A protocol exception occurred.
		/// </exception>
		public abstract Task SendAsync (FormatOptions options, MimeMessage message, MailboxAddress sender, IEnumerable<MailboxAddress> recipients, CancellationToken cancellationToken = default (CancellationToken), ITransferProgress progress = null);

		/// <summary>
		/// Occurs when a message is successfully sent via the transport.
		/// </summary>
		/// <remarks>
		/// The <see cref="MessageSent"/> event will be emitted each time a message is successfully sent.
		/// </remarks>
		public event EventHandler<MessageSentEventArgs> MessageSent;

		/// <summary>
		/// Raise the message sent event.
		/// </summary>
		/// <remarks>
		/// Raises the message sent event.
		/// </remarks>
		/// <param name="e">The message sent event args.</param>
		protected virtual void OnMessageSent (MessageSentEventArgs e)
		{
			var handler = MessageSent;

			if (handler != null)
				handler (this, e);
		}
	}
}
