//
// SmtpUtf8AddressException.cs
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

using MimeKit;
using MimeKit.Utils;

namespace MailKit.Net.Smtp {
	/// <summary>
	/// An exception that is thrown when an address is not a valid SMTPUTF8 address.
	/// </summary>
	/// <remarks>
	/// <para>The exception that is thrown by <see cref="SmtpClient"/> when a message is about to be sent
	/// using the <a href="https://tools.ietf.org/html/rfc6531">SMTPUTF8</a> extension and the sender or one
	/// of the recipients fails validation by <see cref="SmtpUtf8AddressValidator"/>. Only internationalized
	/// addresses (see <see cref="MailboxAddress.IsInternational"/>) are validated.</para>
	/// <para>The addresses are validated before any commands are sent to the server, so no part of the
	/// message will have been sent when this exception is thrown and the <see cref="SmtpClient"/> remains
	/// connected.</para>
	/// </remarks>
	public class SmtpUtf8AddressException : ArgumentException
	{
		/// <summary>
		/// Initializes a new instance of the <see cref="SmtpUtf8AddressException"/> class.
		/// </summary>
		/// <remarks>
		/// Creates a new <see cref="SmtpUtf8AddressException"/>.
		/// </remarks>
		/// <param name="mailbox">The invalid mailbox.</param>
		/// <param name="error">The reason that the mailbox address is invalid.</param>
		/// <param name="message">The error message.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <paramref name="mailbox"/> is <see langword="null" />.
		/// </exception>
		public SmtpUtf8AddressException (MailboxAddress mailbox, SmtpUtf8AddressError error, string message) : base (message)
		{
			if (mailbox == null)
				throw new ArgumentNullException (nameof (mailbox));

			Mailbox = mailbox;
			Error = error;
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="SmtpUtf8AddressException"/> class.
		/// </summary>
		/// <remarks>
		/// Creates a new <see cref="SmtpUtf8AddressException"/> with a default error message.
		/// </remarks>
		/// <param name="mailbox">The invalid mailbox.</param>
		/// <param name="error">The reason that the mailbox address is invalid.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <paramref name="mailbox"/> is <see langword="null" />.
		/// </exception>
		public SmtpUtf8AddressException (MailboxAddress mailbox, SmtpUtf8AddressError error) : this (mailbox, error, GetDefaultMessage (mailbox, error))
		{
		}

		/// <summary>
		/// Get the invalid mailbox.
		/// </summary>
		/// <remarks>
		/// Gets the mailbox whose address is not a valid SMTPUTF8 address.
		/// </remarks>
		/// <value>The invalid mailbox.</value>
		public MailboxAddress Mailbox {
			get; private set;
		}

		/// <summary>
		/// Get the reason that the address is invalid.
		/// </summary>
		/// <remarks>
		/// Gets the reason that the address is not a valid SMTPUTF8 address.
		/// </remarks>
		/// <value>The reason that the address is invalid.</value>
		public SmtpUtf8AddressError Error {
			get; private set;
		}

		static string GetReason (SmtpUtf8AddressError error)
		{
			switch (error) {
			case SmtpUtf8AddressError.Syntax:
				return "the address is syntactically invalid";
			case SmtpUtf8AddressError.LocalPartTooLong:
				return "the local-part is too long";
			case SmtpUtf8AddressError.LabelTooLong:
				return "a domain label is too long";
			case SmtpUtf8AddressError.DomainTooLong:
				return "the domain is too long";
			case SmtpUtf8AddressError.AddressTooLong:
				return "the address is too long";
			case SmtpUtf8AddressError.ALabel:
				return "the address contains an A-label (xn--)";
			case SmtpUtf8AddressError.DisallowedCodePoint:
				return "the address contains a disallowed character";
			case SmtpUtf8AddressError.ContextRule:
				return "the address contains a character that is not permitted in its context";
			case SmtpUtf8AddressError.MixedScripts:
				return "the address mixes characters from more than one script";
			default:
				return error.ToString ();
			}
		}

		static string GetDefaultMessage (MailboxAddress mailbox, SmtpUtf8AddressError error)
		{
			return $"The address <{mailbox?.Address}> is not a valid SMTPUTF8 address: {GetReason (error)}.";
		}
	}
}
