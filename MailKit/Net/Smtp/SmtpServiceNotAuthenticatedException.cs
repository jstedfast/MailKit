//
// SmtpServiceNotAuthenticatedException.cs
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
	/// The exception that is thrown when the SMTP server requires authentication.
	/// </summary>
	/// <remarks>
	/// The exception that is thrown when the SMTP server responds to a command with
	/// <see cref="SmtpStatusCode.AuthenticationRequired"/>.
	/// </remarks>
#if SERIALIZABLE
	[Serializable]
#endif
	public class SmtpServiceNotAuthenticatedException : ServiceNotAuthenticatedException
	{
#if SERIALIZABLE
		/// <summary>
		/// Initializes a new instance of the <see cref="MailKit.Net.Smtp.SmtpServiceNotAuthenticatedException"/> class.
		/// </summary>
		/// <remarks>
		/// Deserializes a <see cref="SmtpServiceNotAuthenticatedException"/>.
		/// </remarks>
		/// <param name="info">The serialization info.</param>
		/// <param name="context">The streaming context.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <paramref name="info"/> is <see langword="null" />.
		/// </exception>
		[SecuritySafeCritical]
		protected SmtpServiceNotAuthenticatedException (SerializationInfo info, StreamingContext context) : base (info, context)
		{
			Command = (SmtpCommand) info.GetInt32 ("Command");
			StatusCode = (SmtpStatusCode) info.GetInt32 ("StatusCode");
			ResponseText = info.GetString ("ResponseText") ?? string.Empty;
		}
#endif

		/// <summary>
		/// Initializes a new instance of the <see cref="MailKit.Net.Smtp.SmtpServiceNotAuthenticatedException"/> class.
		/// </summary>
		/// <remarks>
		/// Creates a new <see cref="SmtpServiceNotAuthenticatedException"/> using the response text as the error message.
		/// </remarks>
		/// <param name="command">The command that the server responded to.</param>
		/// <param name="response">The server's response.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <paramref name="response"/> is <see langword="null" />.
		/// </exception>
		public SmtpServiceNotAuthenticatedException (SmtpCommand command, SmtpResponse response) : base (GetResponseText (response))
		{
			ResponseText = response.ResponseText;
			StatusCode = response.StatusCode;
			Command = command;
		}

		static string GetResponseText (SmtpResponse response)
		{
			if (response == null)
				throw new ArgumentNullException (nameof (response));

			return response.ResponseText;
		}

		/// <summary>
		/// Get the command that resulted in the error.
		/// </summary>
		/// <remarks>
		/// Gets the SMTP command that the server was responding to when the error occurred.
		/// </remarks>
		/// <value>The command.</value>
		public SmtpCommand Command {
			get; private set;
		}

		/// <summary>
		/// Get the status code returned by the SMTP server.
		/// </summary>
		/// <remarks>
		/// Gets the status code returned by the SMTP server.
		/// </remarks>
		/// <value>The status code.</value>
		public SmtpStatusCode StatusCode {
			get; private set;
		}

		/// <summary>
		/// Get the response text returned by the SMTP server.
		/// </summary>
		/// <remarks>
		/// Gets the raw response text (without the status code) returned by the SMTP server.
		/// </remarks>
		/// <value>The response text.</value>
		public string ResponseText {
			get; private set;
		}

#if SERIALIZABLE
		/// <summary>
		/// When overridden in a derived class, sets the <see cref="System.Runtime.Serialization.SerializationInfo"/>
		/// with information about the exception.
		/// </summary>
		/// <remarks>
		/// Serializes the state of the <see cref="SmtpServiceNotAuthenticatedException"/>.
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
			info.AddValue ("StatusCode", (int) StatusCode);
			info.AddValue ("ResponseText", ResponseText);
		}
#endif
	}
}
