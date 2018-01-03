//
// AlertEventArgs.cs
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
	/// Alert event arguments.
	/// </summary>
	/// <remarks>
	/// Some <see cref="IMailStore"/> implementations, such as
	/// <see cref="MailKit.Net.Imap.ImapClient"/>, will emit Alert
	/// events when they receive alert messages from the server.
	/// </remarks>
	public class AlertEventArgs : EventArgs
	{
		/// <summary>
		/// Initializes a new instance of the <see cref="MailKit.AlertEventArgs"/> class.
		/// </summary>
		/// <remarks>
		/// Creates a new <see cref="AlertEventArgs"/>.
		/// </remarks>
		/// <param name="message">The alert message.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <paramref name="message"/> is <c>null</c>.
		/// </exception>
		public AlertEventArgs (string message)
		{
			if (message == null)
				throw new ArgumentNullException (nameof (message));

			Message = message;
		}

		/// <summary>
		/// Gets the alert message.
		/// </summary>
		/// <remarks>
		/// The alert message will be the exact message received from the server.
		/// </remarks>
		/// <value>The alert message.</value>
		public string Message {
			get; private set;
		}
	}
}
