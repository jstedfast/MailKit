//
// SmtpProtocolException.cs
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

namespace MailKit.Net.Smtp {
	/// <summary>
	/// An SMTP protocol exception.
	/// </summary>
	/// <remarks>
	/// The exception that is thrown when there is an error communicating with an SMTP server. An
	/// <see cref="SmtpProtocolException"/> is typically fatal and requires the <see cref="SmtpClient"/>
	/// to be reconnected.
	/// </remarks>
	/// <example>
	/// <code language="c#" source="Examples\SmtpExamples.cs" region="ExceptionHandling"/>
	/// </example>
#if SERIALIZABLE
	[Serializable]
#endif
	public class SmtpProtocolException : ProtocolException
	{
#if SERIALIZABLE
		/// <summary>
		/// Initializes a new instance of the <see cref="MailKit.Net.Smtp.SmtpProtocolException"/> class.
		/// </summary>
		/// <remarks>
		/// Creates a new <see cref="SmtpProtocolException"/> from the serialized data.
		/// </remarks>
		/// <param name="info">The serialization info.</param>
		/// <param name="context">The streaming context.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <paramref name="info"/> is <see langword="null" />.
		/// </exception>
		[SecuritySafeCritical]
		protected SmtpProtocolException (SerializationInfo info, StreamingContext context) : base (info, context)
		{
			Command = (SmtpCommand) info.GetInt32 ("Command");

			var text = info.GetString ("LastResponseText");

			if (text != null)
				LastResponse = new SmtpResponse ((SmtpStatusCode) info.GetInt32 ("LastResponseStatusCode"), text);
		}
#endif

		/// <summary>
		/// Initializes a new instance of the <see cref="MailKit.Net.Smtp.SmtpProtocolException"/> class.
		/// </summary>
		/// <remarks>
		/// Creates a new <see cref="SmtpProtocolException"/>.
		/// </remarks>
		/// <param name="message">The error message.</param>
		/// <param name="errorType">The type of protocol error.</param>
		/// <param name="command">The command that the client was processing when the error occurred.</param>
		/// <param name="lastResponse">The last complete response received from the server for the current command, if any.</param>
		/// <param name="innerException">An inner exception.</param>
		public SmtpProtocolException (string message, ProtocolErrorType errorType, SmtpCommand command, SmtpResponse? lastResponse, Exception innerException) : base (message, errorType, innerException)
		{
			LastResponse = lastResponse;
			Command = command;
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="MailKit.Net.Smtp.SmtpProtocolException"/> class.
		/// </summary>
		/// <remarks>
		/// Creates a new <see cref="SmtpProtocolException"/>.
		/// </remarks>
		/// <param name="message">The error message.</param>
		/// <param name="errorType">The type of protocol error.</param>
		/// <param name="command">The command that the client was processing when the error occurred.</param>
		/// <param name="lastResponse">The last complete response received from the server for the current command, if any.</param>
		public SmtpProtocolException (string message, ProtocolErrorType errorType, SmtpCommand command, SmtpResponse? lastResponse) : base (message, errorType)
		{
			LastResponse = lastResponse;
			Command = command;
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="MailKit.Net.Smtp.SmtpProtocolException"/> class.
		/// </summary>
		/// <remarks>
		/// Creates a new <see cref="SmtpProtocolException"/>.
		/// </remarks>
		/// <param name="message">The error message.</param>
		/// <param name="innerException">The inner exception.</param>
		public SmtpProtocolException (string message, Exception innerException) : base (message, innerException)
		{
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="MailKit.Net.Smtp.SmtpProtocolException"/> class.
		/// </summary>
		/// <remarks>
		/// Creates a new <see cref="SmtpProtocolException"/>.
		/// </remarks>
		/// <param name="message">The error message.</param>
		public SmtpProtocolException (string message) : base (message)
		{
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="MailKit.Net.Smtp.SmtpProtocolException"/> class.
		/// </summary>
		/// <remarks>
		/// Creates a new <see cref="SmtpProtocolException"/>.
		/// </remarks>
		/// <param name="message">The error message.</param>
		/// <param name="errorType">The type of protocol error.</param>
		/// <param name="innerException">An inner exception.</param>
		public SmtpProtocolException (string message, ProtocolErrorType errorType, Exception innerException) : base (message, errorType, innerException)
		{
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="MailKit.Net.Smtp.SmtpProtocolException"/> class.
		/// </summary>
		/// <remarks>
		/// Creates a new <see cref="SmtpProtocolException"/>.
		/// </remarks>
		/// <param name="message">The error message.</param>
		/// <param name="errorType">The type of protocol error.</param>
		public SmtpProtocolException (string message, ProtocolErrorType errorType) : base (message, errorType)
		{
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="MailKit.Net.Smtp.SmtpProtocolException"/> class.
		/// </summary>
		/// <remarks>
		/// Creates a new <see cref="SmtpProtocolException"/>.
		/// </remarks>
		public SmtpProtocolException ()
		{
		}

		/// <summary>
		/// Get the command that the client was processing when the error occurred.
		/// </summary>
		/// <remarks>
		/// <para>Gets the SMTP command that the client was sending, or whose response it was reading,
		/// when the error occurred.</para>
		/// <para>This value will be <see cref="SmtpCommand.Unknown"/> if the exception was created using
		/// a constructor that does not take an <see cref="SmtpCommand"/> argument.</para>
		/// </remarks>
		/// <value>The command.</value>
		public SmtpCommand Command {
			get; private set;
		}

		/// <summary>
		/// Get the last complete response received from the server before the error occurred.
		/// </summary>
		/// <remarks>
		/// <para>Gets the last complete response that was received from the SMTP server before the error
		/// occurred, if any.</para>
		/// <para>The last response is reset each time the client sends a new command, so this value will
		/// only be non-<see langword="null" /> when a response was received after the last command was
		/// sent. For example, if the server disconnects after accepting a <c>MAIL FROM</c> command that was
		/// pipelined with one or more <c>RCPT TO</c> commands, <see cref="Command"/> will be
		/// <see cref="SmtpCommand.RcptTo"/> and this value will be the server's response to the
		/// <c>MAIL FROM</c> command.</para>
		/// </remarks>
		/// <value>The last response, if available; otherwise, <see langword="null" />.</value>
		public SmtpResponse? LastResponse {
			get; private set;
		}

#if SERIALIZABLE
		/// <summary>
		/// When overridden in a derived class, sets the <see cref="System.Runtime.Serialization.SerializationInfo"/>
		/// with information about the exception.
		/// </summary>
		/// <remarks>
		/// Serializes the state of the <see cref="SmtpProtocolException"/>.
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

			info.AddValue ("Command", (int) Command);
			info.AddValue ("LastResponseStatusCode", (int) (LastResponse?.StatusCode ?? 0));
			info.AddValue ("LastResponseText", LastResponse?.ResponseText);
		}
#endif
	}
}
