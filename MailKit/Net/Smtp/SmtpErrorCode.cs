//
// SmtpErrorCode.cs
//
// Author: Jeffrey Stedfast <jestedfa@microsoft.com>
//
// Copyright (c) 2013-2024 .NET Foundation and Contributors
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

namespace MailKit.Net.Smtp {
	/// <summary>
	/// An enumeration of the possible error codes that may be reported by a <see cref="SmtpCommandException"/>.
	/// </summary>
	/// <remarks>
	/// An enumeration of the possible error codes that may be reported by a <see cref="SmtpCommandException"/>.
	/// </remarks>
	/// <example>
	/// <code language="c#" source="Examples\SmtpExamples.cs" region="ExceptionHandling"/>
	/// </example>
	public enum SmtpErrorCode
	{
		/// <summary>
		/// The message was not accepted for delivery. This may happen if
		/// the server runs out of available disk space.
		/// </summary>
		MessageNotAccepted,

		/// <summary>
		/// The sender's mailbox address was not accepted. Check the
		/// <see cref="SmtpCommandException.Mailbox"/> property for the
		/// mailbox used as the sender's mailbox address.
		/// </summary>
		SenderNotAccepted,

		/// <summary>
		/// A recipient's mailbox address was not accepted. Check the
		/// <see cref="SmtpCommandException.Mailbox"/> property for the
		/// particular recipient mailbox that was not acccepted.
		/// </summary>
		RecipientNotAccepted,

		/// <summary>
		/// An unexpected status code was returned by the server.
		/// For more details, the <see cref="Exception.Message"/>
		/// property may provide some additional hints.
		/// </summary>
		UnexpectedStatusCode,
	}
}
