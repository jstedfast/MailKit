//
// ConnectedEventArgs.cs
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

using System;

using MailKit.Security;

namespace MailKit
{
	/// <summary>
	/// Connected event arguments.
	/// </summary>
	/// <remarks>
	/// When a <see cref="IMailService"/> is connected, it will emit a
	/// <see cref=" IMailService.Connected"/> event.
	/// </remarks>
	public class ConnectedEventArgs : EventArgs
	{
		/// <summary>
		/// Initializes a new instance of the <see cref="T:MailKit.ConnectedEventArgs"/> class.
		/// </summary>
		/// <param name="host">The name of the host that the client connected to.</param>
		/// <param name="port">The port that the client connected to on the remote host.</param>
		/// <param name="options">The SSL/TLS options that were used when connecting to the remote host.</param>
		public ConnectedEventArgs (string host, int port, SecureSocketOptions options)
		{
			Options = options;
			Host = host;
			Port = port;
		}

		/// <summary>
		/// Get the name of the remote host.
		/// </summary>
		/// <remarks>
		/// Gets the name of the remote host.
		/// </remarks>
		/// <value>The host name of the server.</value>
		public string Host {
			get; private set;
		}

		/// <summary>
		/// Get the port.
		/// </summary>
		/// <remarks>
		/// Gets the port.
		/// </remarks>
		/// <value>The port.</value>
		public int Port {
			get; private set;
		}

		/// <summary>
		/// Get the SSL/TLS options.
		/// </summary>
		/// <remarks>
		/// Gets the SSL/TLS options.
		/// </remarks>
		/// <value>The SSL/TLS options.</value>
		public SecureSocketOptions Options {
			get; private set;
		}
	}
}
