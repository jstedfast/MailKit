//
// AuthenticatedEventArgs.cs
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
	/// Authenticated event arguments.
	/// </summary>
	/// <remarks>
	/// Some servers, such as GMail IMAP, will send some free-form text in
	/// the response to a successful login.
	/// </remarks>
	public class AuthenticatedEventArgs : EventArgs
	{
		/// <summary>
		/// Initializes a new instance of the <see cref="MailKit.AuthenticatedEventArgs"/> class.
		/// </summary>
		/// <remarks>
		/// Creates a new <see cref="AuthenticatedEventArgs"/>.
		/// </remarks>
		/// <param name="message">The free-form text.</param>
		public AuthenticatedEventArgs (string message)
		{
			Message = message;
		}

		/// <summary>
		/// Get the free-form text sent by the server.
		/// </summary>
		/// <remarks>
		/// Gets the free-form text sent by the server.
		/// </remarks>
		/// <value>The free-form text sent by the server.</value>
		public string Message {
			get; private set;
		}
	}
}
