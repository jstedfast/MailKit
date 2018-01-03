//
// NullProtocolLogger.cs
//
// Author: Jeffrey Stedfast <jestedfa@microsoft.com>
//
// Copyright (c) 2013-2018 Xamarin Inc. (www.xamarin.com)
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

namespace MailKit {
	/// <summary>
	/// A protocol logger that does not log to anywhere.
	/// </summary>
	/// <remarks>
	/// By default, the <see cref="MailKit.Net.Smtp.SmtpClient"/>,
	/// <see cref="MailKit.Net.Pop3.Pop3Client"/>, and
	/// <see cref="MailKit.Net.Imap.ImapClient"/> all use a
	/// <see cref="NullProtocolLogger"/>.
	/// </remarks>
	public sealed class NullProtocolLogger : IProtocolLogger
	{
		/// <summary>
		/// Initializes a new instance of the <see cref="MailKit.NullProtocolLogger"/> class.
		/// </summary>
		/// <remarks>
		/// Creates a new <see cref="NullProtocolLogger"/>.
		/// </remarks>
		public NullProtocolLogger ()
		{
		}

		#region IProtocolLogger implementation

		/// <summary>
		/// Logs a connection to the specified URI.
		/// </summary>
		/// <remarks>
		/// This method does nothing.
		/// </remarks>
		/// <param name="uri">The URI.</param>
		public void LogConnect (Uri uri)
		{
		}

		/// <summary>
		/// Logs a sequence of bytes sent by the client.
		/// </summary>
		/// <remarks>
		/// This method does nothing.
		/// </remarks>
		/// <param name='buffer'>The buffer to log.</param>
		/// <param name='offset'>The offset of the first byte to log.</param>
		/// <param name='count'>The number of bytes to log.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <paramref name="buffer"/> is <c>null</c>.
		/// </exception>
		public void LogClient (byte[] buffer, int offset, int count)
		{
		}

		/// <summary>
		/// Logs a sequence of bytes sent by the server.
		/// </summary>
		/// <remarks>
		/// This method does nothing.
		/// </remarks>
		/// <param name='buffer'>The buffer to log.</param>
		/// <param name='offset'>The offset of the first byte to log.</param>
		/// <param name='count'>The number of bytes to log.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <paramref name="buffer"/> is <c>null</c>.
		/// </exception>
		public void LogServer (byte[] buffer, int offset, int count)
		{
		}

		#endregion

		#region IDisposable implementation

		/// <summary>
		/// Releases all resource used by the <see cref="MailKit.NullProtocolLogger"/> object.
		/// </summary>
		/// <remarks>Call <see cref="Dispose"/> when you are finished using the <see cref="MailKit.NullProtocolLogger"/>. The
		/// <see cref="Dispose"/> method leaves the <see cref="MailKit.NullProtocolLogger"/> in an unusable state. After
		/// calling <see cref="Dispose"/>, you must release all references to the <see cref="MailKit.NullProtocolLogger"/> so
		/// the garbage collector can reclaim the memory that the <see cref="MailKit.NullProtocolLogger"/> was occupying.</remarks>
		public void Dispose ()
		{
		}

		#endregion
	}
}
