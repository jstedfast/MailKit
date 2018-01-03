//
// SmtpCommandException.cs
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
#if SERIALIZABLE
using System.Security;
using System.Runtime.Serialization;
#endif

using MimeKit;

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
	public enum SmtpErrorCode {
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

	/// <summary>
	/// An SMTP protocol exception.
	/// </summary>
	/// <remarks>
	/// The exception that is thrown when an SMTP command fails. Unlike a <see cref="SmtpProtocolException"/>,
	/// a <see cref="SmtpCommandException"/> does not require the <see cref="SmtpClient"/> to be reconnected.
	/// </remarks>
	/// <example>
	/// <code language="c#" source="Examples\SmtpExamples.cs" region="ExceptionHandling"/>
	/// </example>
#if SERIALIZABLE
	[Serializable]
#endif
	public class SmtpCommandException : CommandException
	{
#if SERIALIZABLE
		/// <summary>
		/// Initializes a new instance of the <see cref="MailKit.Net.Smtp.SmtpCommandException"/> class.
		/// </summary>
		/// <remarks>
		/// Creates a new <see cref="SmtpCommandException"/> from the serialized data.
		/// </remarks>
		/// <param name="info">The serialization info.</param>
		/// <param name="context">The streaming context.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <paramref name="info"/> is <c>null</c>.
		/// </exception>
		[SecuritySafeCritical]
		protected SmtpCommandException (SerializationInfo info, StreamingContext context) : base (info, context)
		{
			MailboxAddress mailbox;
			string value;

			value = info.GetString ("Mailbox");
			if (!string.IsNullOrEmpty (value) && MailboxAddress.TryParse (value, out mailbox))
				Mailbox = mailbox;

			ErrorCode = (SmtpErrorCode) info.GetValue ("ErrorCode", typeof (SmtpErrorCode));
			StatusCode = (SmtpStatusCode) info.GetValue ("StatusCode", typeof (SmtpStatusCode));
		}
#endif

		/// <summary>
		/// Initializes a new instance of the <see cref="MailKit.Net.Smtp.SmtpCommandException"/> class.
		/// </summary>
		/// <remarks>
		/// Creates a new <see cref="SmtpCommandException"/>.
		/// </remarks>
		/// <param name="code">The error code.</param>
		/// <param name="status">The status code.</param>
		/// <param name="mailbox">The rejected mailbox.</param>
		/// <param name="message">The error message.</param>
		public SmtpCommandException (SmtpErrorCode code, SmtpStatusCode status, MailboxAddress mailbox, string message) : base (message)
		{
			StatusCode = status;
			Mailbox = mailbox;
			ErrorCode = code;
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="MailKit.Net.Smtp.SmtpCommandException"/> class.
		/// </summary>
		/// <remarks>
		/// Creates a new <see cref="SmtpCommandException"/>.
		/// </remarks>
		/// <param name="code">The error code.</param>
		/// <param name="status">The status code.</param>>
		/// <param name="message">The error message.</param>
		public SmtpCommandException (SmtpErrorCode code, SmtpStatusCode status, string message) : base (message)
		{
			StatusCode = status;
			ErrorCode = code;
		}

#if SERIALIZABLE
		/// <summary>
		/// When overridden in a derived class, sets the <see cref="System.Runtime.Serialization.SerializationInfo"/>
		/// with information about the exception.
		/// </summary>
		/// <remarks>
		/// Serializes the state of the <see cref="SmtpCommandException"/>.
		/// </remarks>
		/// <param name="info">The serialization info.</param>
		/// <param name="context">The streaming context.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <paramref name="info"/> is <c>null</c>.
		/// </exception>
		[SecurityCritical]
		public override void GetObjectData (SerializationInfo info, StreamingContext context)
		{
			base.GetObjectData (info, context);

			if (Mailbox != null)
				info.AddValue ("Mailbox", Mailbox.ToString ());
			else
				info.AddValue ("Mailbox", string.Empty);

			info.AddValue ("ErrorCode", ErrorCode, typeof (SmtpErrorCode));
			info.AddValue ("StatusCode", StatusCode, typeof (SmtpStatusCode));
		}
#endif

		/// <summary>
		/// Gets the error code which may provide additional information.
		/// </summary>
		/// <remarks>
		/// The error code can be used to programatically deal with the
		/// exception without necessarily needing to display the raw
		/// exception message to the user.
		/// </remarks>
		/// <example>
		/// <code language="c#" source="Examples\SmtpExamples.cs" region="ExceptionHandling"/>
		/// </example>
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
		/// <example>
		/// <code language="c#" source="Examples\SmtpExamples.cs" region="ExceptionHandling"/>
		/// </example>
		/// <value>The mailbox.</value>
		public MailboxAddress Mailbox {
			get; private set;
		}

		/// <summary>
		/// Gets the status code returned by the SMTP server.
		/// </summary>
		/// <remarks>
		/// The raw SMTP status code that resulted in the <see cref="SmtpCommandException"/>
		/// being thrown.
		/// </remarks>
		/// <example>
		/// <code language="c#" source="Examples\SmtpExamples.cs" region="ExceptionHandling"/>
		/// </example>
		/// <value>The status code.</value>
		public SmtpStatusCode StatusCode {
			get; private set;
		}
	}
}
