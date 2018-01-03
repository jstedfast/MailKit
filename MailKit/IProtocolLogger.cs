//
// IProtocolLogger.cs
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
	/// An interface for logging protocols.
	/// </summary>
	/// <remarks>
	/// An interface for logging protocols.
	/// </remarks>
	/// <example>
	/// <code language="c#" source="Examples\SmtpExamples.cs" region="ProtocolLogger" />
	/// </example>
	public interface IProtocolLogger : IDisposable
	{
		/// <summary>
		/// Logs a connection to the specified URI.
		/// </summary>
		/// <remarks>
		/// Logs a connection to the specified URI.
		/// </remarks>
		/// <param name="uri">The URI.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <paramref name="uri"/> is <c>null</c>.
		/// </exception>
		/// <exception cref="System.ObjectDisposedException">
		/// The logger has been disposed.
		/// </exception>
		/// <exception cref="System.IO.IOException">
		/// An I/O error occurred.
		/// </exception>
		void LogConnect (Uri uri);

		/// <summary>
		/// Logs a sequence of bytes sent by the client.
		/// </summary>
		/// <remarks>
		/// Logs a sequence of bytes sent by the client.
		/// </remarks>
		/// <param name='buffer'>The buffer to log.</param>
		/// <param name='offset'>The offset of the first byte to log.</param>
		/// <param name='count'>The number of bytes to log.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <paramref name="buffer"/> is <c>null</c>.
		/// </exception>
		/// <exception cref="System.ArgumentOutOfRangeException">
		/// <para><paramref name="offset"/> is less than zero or greater than the length of <paramref name="buffer"/>.</para>
		/// <para>-or-</para>
		/// <para>The <paramref name="buffer"/> is not large enough to contain <paramref name="count"/> bytes strting
		/// at the specified <paramref name="offset"/>.</para>
		/// </exception>
		/// <exception cref="System.ObjectDisposedException">
		/// The logger has been disposed.
		/// </exception>
		/// <exception cref="System.IO.IOException">
		/// An I/O error occurred.
		/// </exception>
		void LogClient (byte[] buffer, int offset, int count);

		/// <summary>
		/// Logs a sequence of bytes sent by the server.
		/// </summary>
		/// <remarks>
		/// Logs a sequence of bytes sent by the server.
		/// </remarks>
		/// <param name='buffer'>The buffer to log.</param>
		/// <param name='offset'>The offset of the first byte to log.</param>
		/// <param name='count'>The number of bytes to log.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <paramref name="buffer"/> is <c>null</c>.
		/// </exception>
		/// <exception cref="System.ArgumentOutOfRangeException">
		/// <para><paramref name="offset"/> is less than zero or greater than the length of <paramref name="buffer"/>.</para>
		/// <para>-or-</para>
		/// <para>The <paramref name="buffer"/> is not large enough to contain <paramref name="count"/> bytes strting
		/// at the specified <paramref name="offset"/>.</para>
		/// </exception>
		/// <exception cref="System.ObjectDisposedException">
		/// The logger has been disposed.
		/// </exception>
		/// <exception cref="System.IO.IOException">
		/// An I/O error occurred.
		/// </exception>
		void LogServer (byte[] buffer, int offset, int count);
	}
}
