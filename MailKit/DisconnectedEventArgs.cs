//
// DisconnectedEventArgs.cs
//
// Author: Jeffrey Stedfast <jestedfa@microsoft.com>
//
// Copyright (c) 2013-2020 .NET Foundation and Contributors
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

using MailKit.Security;

namespace MailKit
{
	/// <summary>
	/// Disconnected event arguments.
	/// </summary>
	/// <remarks>
	/// When a <see cref="IMailService"/> gets disconnected, it will emit a
	/// <see cref=" IMailService.Disconnected"/> event.
	/// </remarks>
	public class DisconnectedEventArgs : ConnectedEventArgs
	{
		/// <summary>
		/// Initializes a new instance of the <see cref="T:MailKit.DisconnectedEventArgs"/> class.
		/// </summary>
		/// <remarks>
		/// Initializes a new instance of the <see cref="T:MailKit.DisconnectedEventArgs"/> class.
		/// </remarks>
		/// <param name="host">The name of the host that the client was connected to.</param>
		/// <param name="port">The port that the client was connected to.</param>
		/// <param name="options">The SSL/TLS options that were used by the client.</param>
		/// <param name="requested">If <c>true</c>, the <see cref="IMailService"/> was disconnected via the
		/// <see cref="IMailService.Disconnect(bool, System.Threading.CancellationToken)"/> method.</param>
		public DisconnectedEventArgs (string host, int port, SecureSocketOptions options, bool requested) : base (host, port, options)
		{
			IsRequested = requested;
		}

		/// <summary>
		/// Get whether or not the service was explicitly asked to disconnect.
		/// </summary>
		/// <remarks>
		/// If the <see cref="IMailService"/> was disconnected via the
		/// <see cref="IMailService.Disconnect(bool, System.Threading.CancellationToken)"/> method, then
		/// the value of <see cref="IsRequested"/> will be <c>true</c>. If the connection was unexpectedly
		/// dropped, then the value will be <c>false</c>.
		/// </remarks>
		/// <value><c>true</c> if the disconnect was explicitly requested; otherwise, <c>false</c>.</value>
		public bool IsRequested {
			get; private set;
		}
	}
}
