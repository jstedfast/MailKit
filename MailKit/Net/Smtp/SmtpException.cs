//
// SmtpException.cs
//
// Author: Jeffrey Stedfast <jeff@xamarin.com>
//
// Copyright (c) 2013 Jeffrey Stedfast
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

using MimeKit;

namespace MailKit.Net.Smtp {
	/// <summary>
	/// An enumeration of the possible error codes that may be reported by a <see cref="SmtpException"/>.
	/// </summary>
	/// <remarks>
	/// Other than the <see cref="SmtpErrorCode.ProtocolError"/> error, none of
	/// the errors require reconnecting to the <see cref="SmtpClient"/> before
	/// continuing to send more messages.
	/// </remarks>
	public enum SmtpErrorCode {
		/// <summary>
		/// The message was not accepted for delivery. This may happen if
		/// the server runs out of available disk space.
		/// </summary>
		MessageNotAccepted,

		/// <summary>
		/// The sender's mailbox address was not accepted. Check the
		/// <see cref="SmtpException.Mailbox"/> property for the
		/// mailbox used as the sender's mailbox address.
		/// </summary>
		SenderNotAccepted,

		/// <summary>
		/// A recipient's mailbox address was not accepted. Check the
		/// <see cref="SmtpException.Mailbox"/> property for the
		/// particular recipient mailbox that was not acccepted.
		/// </summary>
		RecipientNotAccepted,

		/// <summary>
		/// A protocol error occurred. Once a protocol error occurs,
		/// the connection is no longer usable and a new connection
		/// must be made in order to continue sending messages.
		/// </summary>
		ProtocolError,

		/// <summary>
		/// An unexpected status code was returned by the server.
		/// For more details, the <see cref="Exception.Message"/>
		/// property may provide some additional hints.
		/// </summary>
		UnexpectedStatusCode,
	}

	/// <summary>
	/// An SMTP protocol exception.
	/// </summary>
	/// <remarks>
	/// Indicates an error communicating with an SMTP server.
	/// </remarks>
	public class SmtpException : ProtocolException
	{
		/// <summary>
		/// Initializes a new instance of the <see cref="MailKit.Net.Smtp.SmtpException"/> class.
		/// </summary>
		/// <param name="code">The error code.</param>
		/// <param name="status">The status code.</param>
		/// <param name="mailbox">The rejected mailbox.</param>
		/// <param name="message">The error message.</param>
		internal SmtpException (SmtpErrorCode code, SmtpStatusCode status, MailboxAddress mailbox, string message) : base (message)
		{
			StatusCode = (int) status;
			Mailbox = mailbox;
			ErrorCode = code;
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="MailKit.Net.Smtp.SmtpException"/> class.
		/// </summary>
		/// <param name="code">The error code.</param>
		/// <param name="status">The status code.</param>>
		/// <param name="message">The error message.</param>
		internal SmtpException (SmtpErrorCode code, SmtpStatusCode status, string message) : base (message)
		{
			StatusCode = (int) status;
			ErrorCode = code;
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="MailKit.Net.Smtp.SmtpException"/> class.
		/// </summary>
		/// <param name="code">The error code.</param>
		/// <param name="message">The error message.</param>
		internal SmtpException (SmtpErrorCode code, string message) : base (message)
		{
			ErrorCode = code;
			StatusCode = -1;
		}

		/// <summary>
		/// Gets the error code which may provide additional information.
		/// </summary>
		/// <remarks>
		/// The error code can be used to programatically deal with the
		/// exception without necessarily needing to display the raw
		/// exception message to the user.
		/// </remarks>
		/// <value>The status code.</value>
		public SmtpErrorCode ErrorCode {
			get; private set;
		}

		/// <summary>
		/// Gets the mailbox that the error occurred on.
		/// </summary>
		/// <remarks>
		/// This property will only be available when the <see cref="ErrorCode"/>
		/// value is either <see cref="SmtpErrorCode.SenderNotAccepted"/> or
		/// <see cref="SmtpErrorCode.RecipientNotAccepted"/> and may be used
		/// to help the user decide how to proceed.
		/// </remarks>
		/// <value>The mailbox.</value>
		public MailboxAddress Mailbox {
			get; private set;
		}

		/// <summary>
		/// Gets the status code returned by the SMTP server.
		/// </summary>
		/// <remarks>
		/// The raw SMTP status code that resulted in the <see cref="SmtpException"/>
		/// being thrown or <c>-1</c> if the error was a
		/// <see cref="SmtpErrorCode.ProtocolError"/>.
		/// </remarks>
		/// <value>The status code.</value>
		public int StatusCode {
			get; private set;
		}
	}
}
