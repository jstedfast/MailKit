//
// CommandErrorType.cs
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

namespace MailKit {
	/// <summary>
	/// The type of error that caused a <see cref="CommandException"/>.
	/// </summary>
	/// <remarks>
	/// <para>The type of error that caused a <see cref="CommandException"/>.</para>
	/// <para>The error type is derived from protocol-specific information such as IMAP response codes
	/// (<a href="https://tools.ietf.org/html/rfc5530">rfc5530</a>), POP3 response codes
	/// (<a href="https://tools.ietf.org/html/rfc3206">rfc3206</a>) or SMTP status codes.</para>
	/// </remarks>
	public enum CommandErrorType
	{
		/// <summary>
		/// The type of error is unknown.
		/// </summary>
		Unknown,

		/// <summary>
		/// The command was rejected by the server without a more specific reason.
		/// </summary>
		Rejected,

		/// <summary>
		/// The command was invalid or had invalid syntax.
		/// </summary>
		InvalidCommand,

		/// <summary>
		/// The command, or one of its arguments, is not supported by the server.
		/// </summary>
		NotSupported,

		/// <summary>
		/// The user does not have permission to perform the requested operation.
		/// </summary>
		PermissionDenied,

		/// <summary>
		/// The requested resource (e.g. a folder or mailbox) does not exist.
		/// </summary>
		NotFound,

		/// <summary>
		/// The resource that was being created already exists.
		/// </summary>
		AlreadyExists,

		/// <summary>
		/// The operation would exceed the user's storage quota.
		/// </summary>
		QuotaExceeded,

		/// <summary>
		/// The operation would exceed a server-imposed limit (e.g. a maximum message size).
		/// </summary>
		LimitExceeded,

		/// <summary>
		/// The requested resource is currently in use (e.g. locked by another session).
		/// </summary>
		InUse,

		/// <summary>
		/// The server encountered a temporary failure. Retrying the operation later may succeed.
		/// </summary>
		TemporaryFailure,

		/// <summary>
		/// The server encountered an internal error.
		/// </summary>
		ServerError
	}
}
