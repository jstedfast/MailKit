//
// ProtocolLogger.cs
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
using System.IO;
using System.Text;

#if NETFX_CORE
using Encoding = Portable.Text.Encoding;
#endif

namespace MailKit {
	/// <summary>
	/// A protocol logger.
	/// </summary>
	/// <remarks>
	/// A protocol logger.
	/// </remarks>
	/// <example>
	/// <code language="c#" source="Examples\SmtpExamples.cs" region="ProtocolLogger" />
	/// </example>
	public class ProtocolLogger : IProtocolLogger
	{
		static readonly byte[] ClientPrefix = Encoding.ASCII.GetBytes ("C: ");
		static readonly byte[] ServerPrefix = Encoding.ASCII.GetBytes ("S: ");

		readonly Stream stream;
		bool clientMidline;
		bool serverMidline;
		bool leaveOpen;

#if !NETFX_CORE
		/// <summary>
		/// Initializes a new instance of the <see cref="MailKit.ProtocolLogger"/> class.
		/// </summary>
		/// <remarks>
		/// Creates a new <see cref="ProtocolLogger"/> to log to a specified file.
		/// </remarks>
		/// <param name="fileName">The file name.</param>
		public ProtocolLogger (string fileName)
		{
			stream = File.OpenWrite (fileName);
		}
#endif

		/// <summary>
		/// Initializes a new instance of the <see cref="MailKit.ProtocolLogger"/> class.
		/// </summary>
		/// <remarks>
		/// Creates a new <see cref="ProtocolLogger"/> to log to a specified stream.
		/// </remarks>
		/// <param name="stream">The stream.</param>
		/// <param name="leaveOpen"><c>true</c> if the stream should be left open after the protocol logger is disposed.</param>
		public ProtocolLogger (Stream stream, bool leaveOpen = false)
		{
			if (stream == null)
				throw new ArgumentNullException (nameof (stream));

			this.leaveOpen = leaveOpen;
			this.stream = stream;
		}

		/// <summary>
		/// Releases unmanaged resources and performs other cleanup operations before the <see cref="MailKit.ProtocolLogger"/>
		/// is reclaimed by garbage collection.
		/// </summary>
		/// <remarks>
		/// Releases unmanaged resources and performs other cleanup operations before the <see cref="MailKit.ProtocolLogger"/>
		/// is reclaimed by garbage collection.
		/// </remarks>
		~ProtocolLogger ()
		{
			Dispose (false);
		}

		#region IProtocolLogger implementation

		static void ValidateArguments (byte[] buffer, int offset, int count)
		{
			if (buffer == null)
				throw new ArgumentNullException (nameof (buffer));

			if (offset < 0 || offset > buffer.Length)
				throw new ArgumentOutOfRangeException (nameof (offset));

			if (count < 0 || count > (buffer.Length - offset))
				throw new ArgumentOutOfRangeException (nameof (count));
		}

		void Log (byte[] prefix, ref bool midline, byte[] buffer, int offset, int count)
		{
			int endIndex = offset + count;
			int index = offset;
			int start;

			while (index < endIndex) {
				start = index;

				while (index < endIndex && buffer[index] != (byte) '\n')
					index++;

				if (!midline)
					stream.Write (prefix, 0, prefix.Length);

				if (index < endIndex && buffer[index] == (byte) '\n') {
					midline = false;
					index++;
				} else {
					midline = true;
				}

				stream.Write (buffer, start, index - start);
			}

			stream.Flush ();
		}

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
		public void LogConnect (Uri uri)
		{
			if (uri == null)
				throw new ArgumentNullException (nameof (uri));

			var message = string.Format ("Connected to {0}\r\n", uri);
			var buf = Encoding.ASCII.GetBytes (message);

			if (clientMidline || serverMidline) {
				stream.WriteByte ((byte) '\r');
				stream.WriteByte ((byte) '\n');
				clientMidline = false;
				serverMidline = false;
			}

			stream.Write (buf, 0, buf.Length);
			stream.Flush ();
		}

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
		public void LogClient (byte[] buffer, int offset, int count)
		{
			ValidateArguments (buffer, offset, count);

			Log (ClientPrefix, ref clientMidline, buffer, offset, count);
		}

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
		public void LogServer (byte[] buffer, int offset, int count)
		{
			ValidateArguments (buffer, offset, count);

			Log (ServerPrefix, ref serverMidline, buffer, offset, count);
		}

		#endregion

		#region IDisposable implementation

		/// <summary>
		/// Releases the unmanaged resources used by the <see cref="ProtocolLogger"/> and
		/// optionally releases the managed resources.
		/// </summary>
		/// <remarks>
		/// Releases the unmanaged resources used by the <see cref="ProtocolLogger"/> and
		/// optionally releases the managed resources.
		/// </remarks>
		/// <param name="disposing"><c>true</c> to release both managed and unmanaged resources;
		/// <c>false</c> to release only the unmanaged resources.</param>
		protected virtual void Dispose (bool disposing)
		{
			if (disposing && !leaveOpen)
				stream.Dispose ();
		}

		/// <summary>
		/// Releases all resource used by the <see cref="MailKit.ProtocolLogger"/> object.
		/// </summary>
		/// <remarks>Call <see cref="Dispose()"/> when you are finished using the <see cref="MailKit.ProtocolLogger"/>. The
		/// <see cref="Dispose()"/> method leaves the <see cref="MailKit.ProtocolLogger"/> in an unusable state. After calling
		/// <see cref="Dispose()"/>, you must release all references to the <see cref="MailKit.ProtocolLogger"/> so the garbage
		/// collector can reclaim the memory that the <see cref="MailKit.ProtocolLogger"/> was occupying.</remarks>
		public void Dispose ()
		{
			Dispose (true);
			GC.SuppressFinalize (this);
		}

		#endregion
	}
}
