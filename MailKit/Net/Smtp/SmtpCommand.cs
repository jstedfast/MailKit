//
// SmtpCommand.cs
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

namespace MailKit.Net.Smtp {
	/// <summary>
	/// An enumeration of the SMTP commands that the <see cref="SmtpClient"/> may send.
	/// </summary>
	/// <remarks>
	/// <para>An enumeration of the SMTP commands that the <see cref="SmtpClient"/> may send.</para>
	/// <para>This value is used by <see cref="SmtpCommandException"/>, <see cref="SmtpProtocolException"/>, and
	/// <see cref="SmtpServiceNotAuthenticatedException"/> to identify which command (or which response) the
	/// <see cref="SmtpClient"/> was processing when the error occurred.</para>
	/// </remarks>
	/// <example>
	/// <code language="c#" source="Examples\SmtpExamples.cs" region="ExceptionHandling"/>
	/// </example>
	public enum SmtpCommand
	{
		/// <summary>
		/// The command is unknown.
		/// </summary>
		Unknown,

		/// <summary>
		/// Not an actual command, this value represents the greeting sent by the SMTP server upon connecting.
		/// </summary>
		Connect,

		/// <summary>
		/// The <c>EHLO</c> command.
		/// </summary>
		Ehlo,

		/// <summary>
		/// The <c>HELO</c> command.
		/// </summary>
		Helo,

		/// <summary>
		/// The <c>STARTTLS</c> command.
		/// </summary>
		StartTls,

		/// <summary>
		/// The <c>AUTH</c> command (including any SASL challenge responses).
		/// </summary>
		Auth,

		/// <summary>
		/// The <c>MAIL FROM</c> command.
		/// </summary>
		MailFrom,

		/// <summary>
		/// The <c>RCPT TO</c> command.
		/// </summary>
		RcptTo,

		/// <summary>
		/// The <c>DATA</c> command.
		/// </summary>
		Data,

		/// <summary>
		/// Not an actual command, this value represents the message content that is sent after the
		/// server accepts the <c>DATA</c> command, terminated by a line consisting of a single period.
		/// </summary>
		MessageData,

		/// <summary>
		/// The <c>BDAT</c> command.
		/// </summary>
		Bdat,

		/// <summary>
		/// The <c>RSET</c> command.
		/// </summary>
		Rset,

		/// <summary>
		/// The <c>NOOP</c> command.
		/// </summary>
		Noop,

		/// <summary>
		/// The <c>QUIT</c> command.
		/// </summary>
		Quit,

		/// <summary>
		/// The <c>VRFY</c> command.
		/// </summary>
		Vrfy,

		/// <summary>
		/// The <c>EXPN</c> command.
		/// </summary>
		Expn,

		/// <summary>
		/// A custom command sent via <see cref="SmtpClient.SendCommand(string, System.Threading.CancellationToken)"/> or
		/// <see cref="SmtpClient.SendCommandAsync(string, System.Threading.CancellationToken)"/>.
		/// </summary>
		Custom
	}
}
