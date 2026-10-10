//
// IMailTransport.cs
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
using System.Threading;
using System.Threading.Tasks;

using MimeKit;

namespace MailKit {
	/// <summary>
	/// An interface for sending messages.
	/// </summary>
	/// <remarks>
	/// <para>An interface for sending messages.</para>
	/// <para>Implemented by <see cref="MailKit.Net.Smtp.SmtpClient"/>.</para>
	/// </remarks>
	public interface IMailTransport : IMailService
	{
		/// <summary>
		/// Send a message.
		/// </summary>
		/// <remarks>
		/// <para>Sends a message as specified by the request.</para>
		/// <para>If the request does not specify a <see cref="ISendRequest.Sender"/>, the sender address is
		/// determined by checking the following message headers (in order of precedence): Resent-Sender,
		/// Resent-From, Sender, and From.</para>
		/// <para>If the request does not specify any <see cref="ISendRequest.Recipients"/> and either the
		/// Resent-Sender or Resent-From addresses are present, the recipients are collected from the
		/// Resent-To, Resent-Cc, and Resent-Bcc headers, otherwise the To, Cc, and Bcc headers are used.</para>
		/// </remarks>
		/// <returns>The result of sending the message.</returns>
		/// <param name="request">The send request.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		SendResult Send (ISendRequest request, CancellationToken cancellationToken = default);

		/// <summary>
		/// Asynchronously send a message.
		/// </summary>
		/// <remarks>
		/// <para>Asynchronously sends a message as specified by the request.</para>
		/// <para>If the request does not specify a <see cref="ISendRequest.Sender"/>, the sender address is
		/// determined by checking the following message headers (in order of precedence): Resent-Sender,
		/// Resent-From, Sender, and From.</para>
		/// <para>If the request does not specify any <see cref="ISendRequest.Recipients"/> and either the
		/// Resent-Sender or Resent-From addresses are present, the recipients are collected from the
		/// Resent-To, Resent-Cc, and Resent-Bcc headers, otherwise the To, Cc, and Bcc headers are used.</para>
		/// </remarks>
		/// <returns>The result of sending the message.</returns>
		/// <param name="request">The send request.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		Task<SendResult> SendAsync (ISendRequest request, CancellationToken cancellationToken = default);

		/// <summary>
		/// Send a message.
		/// </summary>
		/// <remarks>
		/// <para>Sends a message as specified by the request.</para>
		/// <para>If the request does not specify a <see cref="ISendRequest.Sender"/>, the sender address is
		/// determined by checking the following message headers (in order of precedence): Resent-Sender,
		/// Resent-From, Sender, and From.</para>
		/// <para>If the request does not specify any <see cref="ISendRequest.Recipients"/> and either the
		/// Resent-Sender or Resent-From addresses are present, the recipients are collected from the
		/// Resent-To, Resent-Cc, and Resent-Bcc headers, otherwise the To, Cc, and Bcc headers are used.</para>
		/// </remarks>
		/// <returns>The result of sending the message.</returns>
		/// <param name="options">The formatting options.</param>
		/// <param name="request">The send request.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		SendResult Send (FormatOptions options, ISendRequest request, CancellationToken cancellationToken = default);

		/// <summary>
		/// Asynchronously send a message.
		/// </summary>
		/// <remarks>
		/// <para>Asynchronously sends a message as specified by the request.</para>
		/// <para>If the request does not specify a <see cref="ISendRequest.Sender"/>, the sender address is
		/// determined by checking the following message headers (in order of precedence): Resent-Sender,
		/// Resent-From, Sender, and From.</para>
		/// <para>If the request does not specify any <see cref="ISendRequest.Recipients"/> and either the
		/// Resent-Sender or Resent-From addresses are present, the recipients are collected from the
		/// Resent-To, Resent-Cc, and Resent-Bcc headers, otherwise the To, Cc, and Bcc headers are used.</para>
		/// </remarks>
		/// <returns>The result of sending the message.</returns>
		/// <param name="options">The formatting options.</param>
		/// <param name="request">The send request.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		Task<SendResult> SendAsync (FormatOptions options, ISendRequest request, CancellationToken cancellationToken = default);

		/// <summary>
		/// Occurs when a message is successfully sent via the transport.
		/// </summary>
		/// <remarks>
		/// The <see cref="MessageSent"/> event will be emitted each time a message is successfully sent.
		/// </remarks>
		event EventHandler<MessageSentEventArgs>? MessageSent;
	}
}
