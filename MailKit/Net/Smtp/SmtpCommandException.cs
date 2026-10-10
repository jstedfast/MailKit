//
// SmtpCommandException.cs
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
#if SERIALIZABLE
using System.Security;
using System.Runtime.Serialization;
#endif

using MimeKit;

namespace MailKit.Net.Smtp {
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
		/// <paramref name="info"/> is <see langword="null" />.
		/// </exception>
		[SecuritySafeCritical]
		protected SmtpCommandException (SerializationInfo info, StreamingContext context) : base (info, context)
		{
			var value = info.GetString ("Mailbox");

			if (!string.IsNullOrEmpty (value) && MailboxAddress.TryParse (value, out var mailbox))
				Mailbox = mailbox;

			ErrorCode = (SmtpErrorCode) info.GetValue ("ErrorCode", typeof (SmtpErrorCode));
			StatusCode = (SmtpStatusCode) info.GetValue ("StatusCode", typeof (SmtpStatusCode));
			Command = (SmtpCommand) info.GetValue ("Command", typeof (SmtpCommand));
			ResponseText = info.GetString ("ResponseText");
		}
#endif

		/// <summary>
		/// Initializes a new instance of the <see cref="MailKit.Net.Smtp.SmtpCommandException"/> class.
		/// </summary>
		/// <remarks>
		/// Creates a new <see cref="SmtpCommandException"/> using the response text as the error message.
		/// </remarks>
		/// <param name="code">The error code.</param>
		/// <param name="command">The command that the server responded to.</param>
		/// <param name="response">The server's response.</param>
		/// <param name="mailbox">The rejected mailbox.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <para><paramref name="response"/> is <see langword="null" />.</para>
		/// <para>-or-</para>
		/// <para><paramref name="mailbox"/> is <see langword="null" />.</para>
		/// </exception>
		public SmtpCommandException (SmtpErrorCode code, SmtpCommand command, SmtpResponse response, MailboxAddress mailbox) : this (code, command, response)
		{
			if (mailbox == null)
				throw new ArgumentNullException (nameof (mailbox));

			Mailbox = mailbox;
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="MailKit.Net.Smtp.SmtpCommandException"/> class.
		/// </summary>
		/// <remarks>
		/// Creates a new <see cref="SmtpCommandException"/> using the response text as the error message.
		/// </remarks>
		/// <param name="code">The error code.</param>
		/// <param name="command">The command that the server responded to.</param>
		/// <param name="response">The server's response.</param>
		/// <param name="innerException">The inner exception.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <paramref name="response"/> is <see langword="null" />.
		/// </exception>
		public SmtpCommandException (SmtpErrorCode code, SmtpCommand command, SmtpResponse response, Exception innerException) : base (GetResponseText (response), innerException)
		{
			ResponseText = response.ResponseText;
			StatusCode = response.StatusCode;
			Command = command;
			ErrorCode = code;
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="MailKit.Net.Smtp.SmtpCommandException"/> class.
		/// </summary>
		/// <remarks>
		/// Creates a new <see cref="SmtpCommandException"/> using the response text as the error message.
		/// </remarks>
		/// <param name="code">The error code.</param>
		/// <param name="command">The command that the server responded to.</param>
		/// <param name="response">The server's response.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <paramref name="response"/> is <see langword="null" />.
		/// </exception>
		public SmtpCommandException (SmtpErrorCode code, SmtpCommand command, SmtpResponse response) : base (GetResponseText (response))
		{
			ResponseText = response.ResponseText;
			StatusCode = response.StatusCode;
			Command = command;
			ErrorCode = code;
		}

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
		/// <param name="innerException">The inner exception.</param>
		public SmtpCommandException (SmtpErrorCode code, SmtpStatusCode status, MailboxAddress mailbox, string message, Exception innerException) : base (message, innerException)
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
		/// <param name="innerException">The inner exception.</param>
		public SmtpCommandException (SmtpErrorCode code, SmtpStatusCode status, string message, Exception innerException) : base (message, innerException)
		{
			StatusCode = status;
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

		static string GetResponseText (SmtpResponse response)
		{
			if (response == null)
				throw new ArgumentNullException (nameof (response));

			return response.ResponseText;
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
		/// <paramref name="info"/> is <see langword="null" />.
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
			info.AddValue ("Command", Command, typeof (SmtpCommand));
			info.AddValue ("ResponseText", ResponseText);
		}
#endif

		/// <summary>
		/// Get the command that resulted in the error.
		/// </summary>
		/// <remarks>
		/// <para>Gets the SMTP command that the server was responding to when the error occurred.</para>
		/// <para>This value will be <see cref="SmtpCommand.Unknown"/> if the exception was created using
		/// a constructor that does not take an <see cref="SmtpCommand"/> argument.</para>
		/// </remarks>
		/// <example>
		/// <code language="c#" source="Examples\SmtpExamples.cs" region="ExceptionHandling"/>
		/// </example>
		/// <value>The command.</value>
		public SmtpCommand Command {
			get; internal set;
		}

		/// <summary>
		/// Get the response text returned by the SMTP server.
		/// </summary>
		/// <remarks>
		/// <para>Gets the raw response text (without the status code) returned by the SMTP server.</para>
		/// <para>Unlike <see cref="Exception.Message"/>, this value is never decorated with additional
		/// text and so it can be reliably used for logging or for parsing enhanced status codes.</para>
		/// <para>This value will be <see langword="null" /> if the error was not the result of a server
		/// response or if the exception was created using a constructor that does not take an
		/// <see cref="SmtpResponse"/> argument.</para>
		/// </remarks>
		/// <example>
		/// <code language="c#" source="Examples\SmtpExamples.cs" region="ExceptionHandling"/>
		/// </example>
		/// <value>The response text, if available; otherwise, <see langword="null" />.</value>
		public string? ResponseText {
			get; private set;
		}

		/// <summary>
		/// Get the error code which may provide additional information.
		/// </summary>
		/// <remarks>
		/// The error code can be used to programmatically deal with the
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
		/// Get the mailbox that the error occurred on.
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
		public MailboxAddress? Mailbox {
			get; private set;
		}

		/// <summary>
		/// Get the status code returned by the SMTP server.
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

		/// <summary>
		/// Get the type of command error.
		/// </summary>
		/// <remarks>
		/// Gets the type of command error based on the <see cref="StatusCode"/>.
		/// </remarks>
		/// <value>The type of command error.</value>
		public override CommandErrorType ErrorType {
			get {
				int code = (int) StatusCode;

				switch (code) {
				case 450: return CommandErrorType.InUse;
				case 452: case 552: return CommandErrorType.QuotaExceeded;
				case 500: case 501: case 503: return CommandErrorType.InvalidCommand;
				case 502: case 504: case 555: return CommandErrorType.NotSupported;
				case 530: case 534: case 535: case 538: return CommandErrorType.PermissionDenied;
				}

				if (code >= 400 && code < 500)
					return CommandErrorType.TemporaryFailure;

				if (code >= 500 && code < 600)
					return CommandErrorType.Rejected;

				return CommandErrorType.Unknown;
			}
		}
	}
}
