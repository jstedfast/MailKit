//
// SmtpCapabilities.cs
//
// Author: Jeffrey Stedfast <jestedfa@microsoft.com>
//
// Copyright (c) 2013-2018 Xamarin Inc. (www.xamarin.com)
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
	/// <code language="c#" source="Examples\SmtpExamples.cs" region="Capabilities"/>
	/// </example>
	[Flags]
	public enum SmtpCapabilities {
		/// <summary>
		/// The server does not support any additional extensions.
		/// </summary>
		None                = 0,

		/// <summary>
		/// The server supports the <a href="https://tools.ietf.org/html/rfc1870">SIZE</a> extension
		/// and may have a maximum message size limitation (see <see cref="SmtpClient.MaxSize"/>).
		/// </summary>
		Size                = (1 << 0),

		/// <summary>
		/// The server supports the <a href="https://tools.ietf.org/html/rfc1891">DSN</a> extension,
		/// allowing clients to specify which (if any) recipients they would like to receive delivery
		/// notifications for.
		/// </summary>
		Dsn                 = (1 << 1),

		/// <summary>
		/// The server supports the <a href="https://tools.ietf.org/html/rfc2034">ENHANCEDSTATUSCODES</a>
		/// extension.
		/// </summary>
		EnhancedStatusCodes = (1 << 2),

		/// <summary>
		/// The server supports the <a href="https://tools.ietf.org/html/rfc2554">AUTH</a> extension,
		/// allowing clients to authenticate via supported SASL mechanisms.
		/// </summary>
		Authentication      = (1 << 3),

		/// <summary>
		/// The server supports the <a href="https://tools.ietf.org/html/rfc2821">8BITMIME</a> extension,
		/// allowing clients to send messages using the "8bit" Content-Transfer-Encoding.
		/// </summary>
		EightBitMime        = (1 << 4),

		/// <summary>
		/// The server supports the <a href="https://tools.ietf.org/html/rfc2920">PIPELINING</a> extension,
		/// allowing clients to send multiple commands at once in order to reduce round-trip latency.
		/// </summary>
		Pipelining          = (1 << 5),

		/// <summary>
		/// The server supports the <a href="https://tools.ietf.org/html/rfc3030">BINARYMIME</a> extension.
		/// </summary>
		BinaryMime          = (1 << 6),

		/// <summary>
		/// The server supports the <a href="https://tools.ietf.org/html/rfc3030">CHUNKING</a> extension,
		/// allowing clients to upload messages in chunks.
		/// </summary>
		Chunking            = (1 << 7),

		/// <summary>
		/// The server supports the <a href="https://tools.ietf.org/html/rfc3207">STARTTLS</a> extension,
		/// allowing clients to switch to an encrypted SSL/TLS connection after connecting.
		/// </summary>
		StartTLS            = (1 << 8),

		/// <summary>
		/// The server supports the <a href="https://tools.ietf.org/html/rfc6531">SMTPUTF8</a> extension.
		/// </summary>
		UTF8                = (1 << 9),
	}
}
