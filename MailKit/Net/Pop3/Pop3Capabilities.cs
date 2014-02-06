//
// Pop3Capabilities.cs
//
// Author: Jeffrey Stedfast <jeff@xamarin.com>
//
// Copyright (c) 2013-2014 Xamarin Inc. (www.xamarin.com)
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

namespace MailKit.Net.Pop3 {
	/// <summary>
	/// Capabilities supported by a POP3 server.
	/// </summary>
	/// <remarks>
	/// Capabilities are read as part of the response to the CAPA command that
	/// is issued during the connection and authentication phases of the
	/// <see cref="Pop3Client"/>.
	/// </remarks>
	[Flags]
	public enum Pop3Capabilities {
		/// <summary>
		/// The server does not support any additional extensions.
		/// </summary>
		None                   = 0,

		/// <summary>
		/// The server supports APOP authentication (rfc1939).
		/// </summary>
		Apop                   = (1 << 0),

		/// <summary>
		/// The server supports the EXPIRE extension (rfc2449) and defines the
		/// expiration policy for messages (see <see cref="Pop3Client.ExpirePolicy"/>).
		/// </summary>
		Expire                 = (1 << 1),

		/// <summary>
		/// The server supports the LOGIN-DELAY extension (rfc2449), allowing the
		/// server to specify to the client a minimum number of seconds between
		/// login attempts (see <see cref="Pop3Client.LoginDelay"/>).
		/// </summary>
		LoginDelay             = (1 << 2),

		/// <summary>
		/// The server supports the PIPELINING extensions (rfc2449), allowing the
		/// client to batch multiple requests to the server at at time.
		/// </summary>
		Pipelining             = (1 << 3),

		/// <summary>
		/// The server supports the RESP-CODES extension (rfc2449), allowing the server
		/// to provide clients with extended information in error responses.
		/// </summary>
		ResponseCodes          = (1 << 4),

		/// <summary>
		/// The server supports the SASL authentication extension (rfc2449), allowing
		/// the client to authenticate using the advertized authentication mechanisms
		/// (see <see cref="Pop3Client.AuthenticationMechanisms"/>).
		/// </summary>
		Sasl                   = (1 << 5),

		/// <summary>
		/// The server supports the STLS extension (rfc2595), allowing clients
		/// to switch to an encrypted SSL/TLS connection after connecting.
		/// </summary>
		StartTLS               = (1 << 6),

		/// <summary>
		/// The server supports the TOP command, allowing clients to fetch the
		/// headers plus an arbitrary number of lines.
		/// </summary>
		Top                    = (1 << 7),

		/// <summary>
		/// The server supports the UIDL command, allowing the client to refer to
		/// messages via a UID as opposed to a sequence ID.
		/// </summary>
		UIDL                   = (1 << 8),

		/// <summary>
		/// The server supports the USER authentication command, allowing the
		/// client to authenticate via a plain-text username and password command
		/// (not recommended unless no other authentication mechanisms exist).
		/// </summary>
		User                   = (1 << 9),
	}
}
