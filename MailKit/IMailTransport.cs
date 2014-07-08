//
// IMailTransport.cs
//
// Author: Jeffrey Stedfast <jeff@xamarin.com>
//
// Copyright (c) 2013-2014 Xamarin Inc. (www.xamarin.com)
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
		/// Sends the specified message.
		/// </remarks>
		/// <param name="message">The message.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		void Send (MimeMessage message, CancellationToken cancellationToken = default (CancellationToken));

		/// <summary>
		/// Asynchronously send the specified message.
		/// </summary>
		/// <remarks>
		/// Asynchronously sends the specified message.
		/// </remarks>
		/// <returns>An asynchronous task context.</returns>
		/// <param name="message">The message.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		Task SendAsync (MimeMessage message, CancellationToken cancellationToken = default (CancellationToken));

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
		void Send (MimeMessage message, MailboxAddress sender, IEnumerable<MailboxAddress> recipients, CancellationToken cancellationToken = default (CancellationToken));

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
		Task SendAsync (MimeMessage message, MailboxAddress sender, IEnumerable<MailboxAddress> recipients, CancellationToken cancellationToken = default (CancellationToken));
	}
}
