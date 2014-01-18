//
// ImapException.cs
//
// Author: Jeffrey Stedfast <jeff@xamarin.com>
//
// Copyright (c) 2014 Jeffrey Stedfast
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

namespace MailKit.Net.Imap {
	/// <summary>
	/// An enumeration of the possible types of IMAP errors.
	/// </summary>
	public enum ImapErrorType {
		/// <summary>
		/// There was a fatal protocol error.
		/// </summary>
		ProtocolError,

		/// <summary>
		/// The IMAP server replied with <c>"BAD"</c> to a command.
		/// </summary>
		CommandError,

		/// <summary>
		/// An error occurred while parsing the response from the server.
		/// </summary>
		ParseError,
	}

	/// <summary>
	/// A IMAP protocol exception.
	/// </summary>
	/// <remarks>
	/// The exception that is thrown when there is an error communicating with a IMAP server.
	/// </remarks>
	public class ImapException : ProtocolException
	{
		/// <summary>
		/// Initializes a new instance of the <see cref="MailKit.Net.Imap.ImapException"/> class.
		/// </summary>
		/// <param name="type">The error type.</param>
		/// <param name="message">The error message.</param>
		/// <param name="innerException">An inner exception.</param>
		internal ImapException (ImapErrorType type, string message, Exception innerException) : base (message, innerException)
		{
			ErrorType = type;
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="MailKit.Net.Imap.ImapException"/> class.
		/// </summary>
		/// <param name="type">The error type.</param>
		/// <param name="message">The error message.</param>
		internal ImapException (ImapErrorType type, string message) : base (message)
		{
			ErrorType = type;
		}

		/// <summary>
		/// Gets the type of the error.
		/// </summary>
		/// <remarks>
		/// <see cref="ImapErrorType.ProtocolError"/> always requires the client to reconnect before coninuing.
		/// </remarks>
		/// <value>The type of the error.</value>
		public ImapErrorType ErrorType {
			get; private set;
		}
	}
}

