//
// IMailTransportExtensions.cs
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


using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;

using MimeKit;

namespace MailKit {
	/// <summary>
	/// Extension methods for <see cref="IMailTransport"/>.
	/// </summary>
	/// <remarks>
	/// Extension methods for <see cref="IMailTransport"/>.
	/// </remarks>
	public static class IMailTransportExtensions
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
		/// <example>
		/// <code language="c#" source="Examples\SmtpExamples.cs" region="SendMessage"/>
		/// </example>
		/// <returns>The result of sending the message.</returns>
		/// <param name="transport">The transport.</param>
		/// <param name="message">The message.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <param name="progress">The progress reporting mechanism.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <paramref name="message"/> is <see langword="null" />.
		/// </exception>
		/// <exception cref="System.ObjectDisposedException">
		/// The <see cref="IMailTransport"/> has been disposed.
		/// </exception>
		/// <exception cref="ServiceNotConnectedException">
		/// The <see cref="IMailTransport"/> is not connected.
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
		public static SendResult Send (this IMailTransport transport, MimeMessage message, CancellationToken cancellationToken = default, ITransferProgress? progress = null)
		{
			var request = new SendRequest (message) {
				TransferProgress = progress
			};

			return transport.Send (request, cancellationToken);
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
		/// <example>
		/// <code language="c#" source="Examples\SmtpExamples.cs" region="SendMessage"/>
		/// </example>
		/// <returns>The result of sending the message.</returns>
		/// <param name="transport">The transport.</param>
		/// <param name="message">The message.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <param name="progress">The progress reporting mechanism.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <paramref name="message"/> is <see langword="null" />.
		/// </exception>
		/// <exception cref="System.ObjectDisposedException">
		/// The <see cref="IMailTransport"/> has been disposed.
		/// </exception>
		/// <exception cref="ServiceNotConnectedException">
		/// The <see cref="IMailTransport"/> is not connected.
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
		public static Task<SendResult> SendAsync (this IMailTransport transport, MimeMessage message, CancellationToken cancellationToken = default, ITransferProgress? progress = null)
		{
			var request = new SendRequest (message) {
				TransferProgress = progress
			};

			return transport.SendAsync (request, cancellationToken);
		}

		/// <summary>
		/// Send the specified message using the supplied sender and recipients.
		/// </summary>
		/// <remarks>
		/// Sends the specified message using the supplied sender and recipients.
		/// </remarks>
		/// <returns>The result of sending the message.</returns>
		/// <param name="transport">The transport.</param>
		/// <param name="message">The message.</param>
		/// <param name="sender">The mailbox address to use for sending the message.</param>
		/// <param name="recipients">The mailbox addresses that should receive the message.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <param name="progress">The progress reporting mechanism.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <para><paramref name="message"/> is <see langword="null" />.</para>
		/// <para>-or-</para>
		/// <para><paramref name="sender"/> is <see langword="null" />.</para>
		/// <para>-or-</para>
		/// <para><paramref name="recipients"/> is <see langword="null" />.</para>
		/// </exception>
		/// <exception cref="System.ObjectDisposedException">
		/// The <see cref="IMailTransport"/> has been disposed.
		/// </exception>
		/// <exception cref="ServiceNotConnectedException">
		/// The <see cref="IMailTransport"/> is not connected.
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
		public static SendResult Send (this IMailTransport transport, MimeMessage message, MailboxAddress sender, IEnumerable<MailboxAddress> recipients, CancellationToken cancellationToken = default, ITransferProgress? progress = null)
		{
			var request = new SendRequest (message, sender, recipients) {
				TransferProgress = progress
			};

			return transport.Send (request, cancellationToken);
		}

		/// <summary>
		/// Asynchronously send the specified message using the supplied sender and recipients.
		/// </summary>
		/// <remarks>
		/// Asynchronously sends the specified message using the supplied sender and recipients.
		/// </remarks>
		/// <returns>The result of sending the message.</returns>
		/// <param name="transport">The transport.</param>
		/// <param name="message">The message.</param>
		/// <param name="sender">The mailbox address to use for sending the message.</param>
		/// <param name="recipients">The mailbox addresses that should receive the message.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <param name="progress">The progress reporting mechanism.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <para><paramref name="message"/> is <see langword="null" />.</para>
		/// <para>-or-</para>
		/// <para><paramref name="sender"/> is <see langword="null" />.</para>
		/// <para>-or-</para>
		/// <para><paramref name="recipients"/> is <see langword="null" />.</para>
		/// </exception>
		/// <exception cref="System.ObjectDisposedException">
		/// The <see cref="IMailTransport"/> has been disposed.
		/// </exception>
		/// <exception cref="ServiceNotConnectedException">
		/// The <see cref="IMailTransport"/> is not connected.
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
		public static Task<SendResult> SendAsync (this IMailTransport transport, MimeMessage message, MailboxAddress sender, IEnumerable<MailboxAddress> recipients, CancellationToken cancellationToken = default, ITransferProgress? progress = null)
		{
			var request = new SendRequest (message, sender, recipients) {
				TransferProgress = progress
			};

			return transport.SendAsync (request, cancellationToken);
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
		/// <returns>The result of sending the message.</returns>
		/// <param name="transport">The transport.</param>
		/// <param name="options">The formatting options.</param>
		/// <param name="message">The message.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <param name="progress">The progress reporting mechanism.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <para><paramref name="options"/> is <see langword="null" />.</para>
		/// <para>-or-</para>
		/// <para><paramref name="message"/> is <see langword="null" />.</para>
		/// </exception>
		/// <exception cref="System.ObjectDisposedException">
		/// The <see cref="IMailTransport"/> has been disposed.
		/// </exception>
		/// <exception cref="ServiceNotConnectedException">
		/// The <see cref="IMailTransport"/> is not connected.
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
		public static SendResult Send (this IMailTransport transport, FormatOptions options, MimeMessage message, CancellationToken cancellationToken = default, ITransferProgress? progress = null)
		{
			var request = new SendRequest (message) {
				TransferProgress = progress
			};

			return transport.Send (options, request, cancellationToken);
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
		/// <example>
		/// <code language="c#" source="Examples\SmtpExamples.cs" region="SendMessageWithOptions"/>
		/// </example>
		/// <returns>The result of sending the message.</returns>
		/// <param name="transport">The transport.</param>
		/// <param name="options">The formatting options.</param>
		/// <param name="message">The message.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <param name="progress">The progress reporting mechanism.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <para><paramref name="options"/> is <see langword="null" />.</para>
		/// <para>-or-</para>
		/// <para><paramref name="message"/> is <see langword="null" />.</para>
		/// </exception>
		/// <exception cref="System.ObjectDisposedException">
		/// The <see cref="IMailTransport"/> has been disposed.
		/// </exception>
		/// <exception cref="ServiceNotConnectedException">
		/// The <see cref="IMailTransport"/> is not connected.
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
		public static Task<SendResult> SendAsync (this IMailTransport transport, FormatOptions options, MimeMessage message, CancellationToken cancellationToken = default, ITransferProgress? progress = null)
		{
			var request = new SendRequest (message) {
				TransferProgress = progress
			};

			return transport.SendAsync (options, request, cancellationToken);
		}

		/// <summary>
		/// Send the specified message using the supplied sender and recipients.
		/// </summary>
		/// <remarks>
		/// Sends the specified message using the supplied sender and recipients.
		/// </remarks>
		/// <returns>The result of sending the message.</returns>
		/// <param name="transport">The transport.</param>
		/// <param name="options">The formatting options.</param>
		/// <param name="message">The message.</param>
		/// <param name="sender">The mailbox address to use for sending the message.</param>
		/// <param name="recipients">The mailbox addresses that should receive the message.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <param name="progress">The progress reporting mechanism.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <para><paramref name="options"/> is <see langword="null" />.</para>
		/// <para>-or-</para>
		/// <para><paramref name="message"/> is <see langword="null" />.</para>
		/// <para>-or-</para>
		/// <para><paramref name="sender"/> is <see langword="null" />.</para>
		/// <para>-or-</para>
		/// <para><paramref name="recipients"/> is <see langword="null" />.</para>
		/// </exception>
		/// <exception cref="System.ObjectDisposedException">
		/// The <see cref="IMailTransport"/> has been disposed.
		/// </exception>
		/// <exception cref="ServiceNotConnectedException">
		/// The <see cref="IMailTransport"/> is not connected.
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
		public static SendResult Send (this IMailTransport transport, FormatOptions options, MimeMessage message, MailboxAddress sender, IEnumerable<MailboxAddress> recipients, CancellationToken cancellationToken = default, ITransferProgress? progress = null)
		{
			var request = new SendRequest (message, sender, recipients) {
				TransferProgress = progress
			};

			return transport.Send (options, request, cancellationToken);
		}

		/// <summary>
		/// Asynchronously send the specified message using the supplied sender and recipients.
		/// </summary>
		/// <remarks>
		/// Asynchronously sends the specified message using the supplied sender and recipients.
		/// </remarks>
		/// <returns>The result of sending the message.</returns>
		/// <param name="transport">The transport.</param>
		/// <param name="options">The formatting options.</param>
		/// <param name="message">The message.</param>
		/// <param name="sender">The mailbox address to use for sending the message.</param>
		/// <param name="recipients">The mailbox addresses that should receive the message.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <param name="progress">The progress reporting mechanism.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <para><paramref name="options"/> is <see langword="null" />.</para>
		/// <para>-or-</para>
		/// <para><paramref name="message"/> is <see langword="null" />.</para>
		/// <para>-or-</para>
		/// <para><paramref name="sender"/> is <see langword="null" />.</para>
		/// <para>-or-</para>
		/// <para><paramref name="recipients"/> is <see langword="null" />.</para>
		/// </exception>
		/// <exception cref="System.ObjectDisposedException">
		/// The <see cref="IMailTransport"/> has been disposed.
		/// </exception>
		/// <exception cref="ServiceNotConnectedException">
		/// The <see cref="IMailTransport"/> is not connected.
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
		public static Task<SendResult> SendAsync (this IMailTransport transport, FormatOptions options, MimeMessage message, MailboxAddress sender, IEnumerable<MailboxAddress> recipients, CancellationToken cancellationToken = default, ITransferProgress? progress = null)
		{
			var request = new SendRequest (message, sender, recipients) {
				TransferProgress = progress
			};

			return transport.SendAsync (options, request, cancellationToken);
		}
	}
}
