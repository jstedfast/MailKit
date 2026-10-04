//
// Pop3CommandException.cs
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

namespace MailKit.Net.Pop3 {
	/// <summary>
	/// A POP3 command exception.
	/// </summary>
	/// <remarks>
	/// The exception that is thrown when a POP3 command fails. Unlike a <see cref="Pop3ProtocolException"/>,
	/// a <see cref="Pop3CommandException"/> does not require the <see cref="Pop3Client"/> to be reconnected.
	/// </remarks>
	/// <example>
	/// <code language="c#" source="Examples\Pop3Examples.cs" region="ExceptionHandling"/>
	/// </example>
#if SERIALIZABLE
	[Serializable]
#endif
	public class Pop3CommandException : CommandException
	{
#if SERIALIZABLE
		/// <summary>
		/// Initializes a new instance of the <see cref="MailKit.Net.Pop3.Pop3CommandException"/> class.
		/// </summary>
		/// <remarks>
		/// Creates a new <see cref="Pop3CommandException"/> from the serialized data.
		/// </remarks>
		/// <param name="info">The serialization info.</param>
		/// <param name="context">The streaming context.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <paramref name="info"/> is <see langword="null" />.
		/// </exception>
		[SecuritySafeCritical]
		protected Pop3CommandException (SerializationInfo info, StreamingContext context) : base (info, context)
		{
			StatusText = info.GetString ("StatusText");
		}
#endif

		/// <summary>
		/// Initializes a new instance of the <see cref="MailKit.Net.Pop3.Pop3CommandException"/> class.
		/// </summary>
		/// <remarks>
		/// Creates a new <see cref="Pop3CommandException"/>.
		/// </remarks>
		/// <param name="message">The error message.</param>
		/// <param name="innerException">An inner exception.</param>
		public Pop3CommandException (string message, Exception innerException) : base (message, innerException)
		{
			StatusText = string.Empty;
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="MailKit.Net.Pop3.Pop3CommandException"/> class.
		/// </summary>
		/// <remarks>
		/// Creates a new <see cref="Pop3CommandException"/>.
		/// </remarks>
		/// <param name="message">The error message.</param>
		/// <param name="statusText">The response status text.</param>
		/// <param name="innerException">An inner exception.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <paramref name="statusText"/> is <see langword="null" />.
		/// </exception>
		public Pop3CommandException (string message, string statusText, Exception innerException) : base (message, innerException)
		{
			if (statusText == null)
				throw new ArgumentNullException (nameof (statusText));

			StatusText = statusText;
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="MailKit.Net.Pop3.Pop3CommandException"/> class.
		/// </summary>
		/// <remarks>
		/// Creates a new <see cref="Pop3CommandException"/>.
		/// </remarks>
		/// <param name="message">The error message.</param>
		public Pop3CommandException (string message) : base (message)
		{
			StatusText = string.Empty;
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="MailKit.Net.Pop3.Pop3CommandException"/> class.
		/// </summary>
		/// <remarks>
		/// Creates a new <see cref="Pop3CommandException"/>.
		/// </remarks>
		/// <param name="message">The error message.</param>
		/// <param name="statusText">The response status text.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <paramref name="statusText"/> is <see langword="null" />.
		/// </exception>
		public Pop3CommandException (string message, string statusText) : base (message)
		{
			if (statusText == null)
				throw new ArgumentNullException (nameof (statusText));

			StatusText = statusText;
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="MailKit.Net.Pop3.Pop3CommandException"/> class.
		/// </summary>
		/// <remarks>
		/// Creates a new <see cref="Pop3CommandException"/>.
		/// </remarks>
		public Pop3CommandException ()
		{
			StatusText = string.Empty;
		}

		/// <summary>
		/// Get the response status text.
		/// </summary>
		/// <remarks>
		/// Gets the response status text.
		/// </remarks>
		/// <example>
		/// <code language="c#" source="Examples\Pop3Examples.cs" region="ExceptionHandling"/>
		/// </example>
		/// <value>The response status text.</value>
		public string StatusText {
			get; private set;
		}

		/// <summary>
		/// Get the POP3 response code, if any.
		/// </summary>
		/// <remarks>
		/// <para>Gets the extended response code that the server included at the beginning of
		/// the <see cref="StatusText"/>, if any.</para>
		/// <para>Extended response codes are defined by
		/// <a href="https://tools.ietf.org/html/rfc2449">rfc2449</a> and
		/// <a href="https://tools.ietf.org/html/rfc3206">rfc3206</a> and include values such as
		/// <c>"IN-USE"</c>, <c>"LOGIN-DELAY"</c>, <c>"SYS/TEMP"</c>, <c>"SYS/PERM"</c> and <c>"AUTH"</c>.</para>
		/// </remarks>
		/// <value>The upper-case response code or <see langword="null" /> if the server did not include a response code.</value>
		public string? ResponseCode {
			get {
				if (StatusText.Length < 3 || StatusText[0] != '[')
					return null;

				int endIndex = StatusText.IndexOf (']', 1);

				if (endIndex <= 1)
					return null;

				return StatusText.Substring (1, endIndex - 1).Trim ().ToUpperInvariant ();
			}
		}

		/// <summary>
		/// Get the type of command error.
		/// </summary>
		/// <remarks>
		/// Gets the type of command error based on the <see cref="ResponseCode"/>.
		/// </remarks>
		/// <value>The type of command error.</value>
		public override CommandErrorType ErrorType {
			get {
				var code = ResponseCode;

				if (code is null)
					return CommandErrorType.Rejected;

				if (code == "IN-USE")
					return CommandErrorType.InUse;

				if (code == "LOGIN-DELAY" || code == "SYS/TEMP" || code.StartsWith ("SYS/TEMP/", StringComparison.Ordinal))
					return CommandErrorType.TemporaryFailure;

				if (code == "SYS/PERM" || code.StartsWith ("SYS/PERM/", StringComparison.Ordinal))
					return CommandErrorType.ServerError;

				if (code == "AUTH" || code.StartsWith ("AUTH/", StringComparison.Ordinal))
					return CommandErrorType.PermissionDenied;

				return CommandErrorType.Rejected;
			}
		}

#if SERIALIZABLE
		/// <summary>
		/// When overridden in a derived class, sets the <see cref="System.Runtime.Serialization.SerializationInfo"/>
		/// with information about the exception.
		/// </summary>
		/// <remarks>
		/// Serializes the state of the <see cref="FolderNotFoundException"/>.
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

			info.AddValue ("StatusText", StatusText);
		}
#endif
	}
}
