//
// SmtpCapabilities.cs
//
// Author: Jeffrey Stedfast <jeff@xamarin.com>
//
// Copyright (c) 2013-2015 Xamarin Inc. (www.xamarin.com)
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitations the rights
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

namespace MailKit.Net.Smtp {
	/// <summary>
	/// Capabilities supported by an SMTP server.
	/// </summary>
	/// <remarks>
	/// Capabilities are read as part of the response to the EHLO command that
	/// is issued during the connection phase of the <see cref="SmtpClient"/>.
	/// </remarks>
	/// <example>
	/// <code language="c#" source="Examples\SmtpExamples.cs" region="SendMessageWithOptions"/>
	/// </example>
	[Flags]
	public enum SmtpCapabilities {
		/// <summary>
		/// The server does not support any additional extensions.
		/// </summary>
		None                = 0,

		/// <summary>
		/// The server supports the SIZE extension (rfc1870) and may have a maximum
		/// message size limitation (see <see cref="SmtpClient.MaxSize"/>).
		/// </summary>
		Size                = (1 << 0),

		/// <summary>
		/// The server supports the DSN extension (rfc1891), allowing clients to
		/// specify which (if any) recipients they would like to receive delivery
		/// notifications for.
		/// </summary>
		Dsn                 = (1 << 1),

		/// <summary>
		/// The server supports the ENHANCEDSTATUSCODES extension (rfc2034).
		/// </summary>
		EnhancedStatusCodes = (1 << 2),

		/// <summary>
		/// The server supports the AUTH extension (rfc2554), allowing clients to
		/// authenticate via supported SASL mechanisms.
		/// </summary>
		Authentication      = (1 << 3),

		/// <summary>
		/// The server supports the 8BITMIME extension (rfc2821), allowing clients
		/// to send messages using the "8bit" Content-Transfer-Encoding.
		/// </summary>
		EightBitMime        = (1 << 4),

		/// <summary>
		/// The server supports the PIPELINING extension (rfc2920), allowing clients
		/// to send multiple commands at once in order to reduce round-trip latency.
		/// </summary>
		Pipelining          = (1 << 5),

		/// <summary>
		/// The server supports the BINARYMIME extension (rfc3030).
		/// </summary>
		BinaryMime          = (1 << 6),

		/// <summary>
		/// The server supports the CHUNKING extension (rfc3030), allowing clients
		/// to upload messages in chunks.
		/// </summary>
		Chunking            = (1 << 7),

		/// <summary>
		/// The server supports the STARTTLS extension (rfc3207), allowing clients
		/// to switch to an encrypted SSL/TLS connection after connecting.
		/// </summary>
		StartTLS            = (1 << 8),

		/// <summary>
		/// The server supports the SMTPUTF8 extension (rfc6531).
		/// </summary>
		UTF8                = (1 << 9),
	}
}
