//
// WebAlertEventArgs.cs
//
// Author: Jeffrey Stedfast <jestedfa@microsoft.com>
//
// Copyright (c) 2013-2021 .NET Foundation and Contributors
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

namespace MailKit
{
	/// <summary>
	/// Alert event arguments.
	/// </summary>
	/// <remarks>
	/// Some <see cref="IMailStore"/> implementations, such as
	/// <see cref="MailKit.Net.Imap.ImapClient"/>, will emit WebAlert
	/// events when they receive web alert messages from the server.
	/// </remarks>
	public class WebAlertEventArgs : AlertEventArgs
	{
		/// <summary>
		/// Initializes a new instance of the <see cref="MailKit.WebAlertEventArgs"/> class.
		/// </summary>
		/// <remarks>
		/// Creates a new <see cref="WebAlertEventArgs"/>.
		/// </remarks>
		/// <param name="uri">The web URI.</param>
		/// <param name="message">The alert message.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <para><paramref name="uri"/> is <c>null</c>.</para>
		/// <para>-or-</para>
		/// <para><paramref name="message"/> is <c>null</c>.</para>
		/// </exception>
		public WebAlertEventArgs (Uri uri, string message) : base (message)
		{
			if (uri == null)
				throw new ArgumentNullException (nameof (uri));

			WebUri = uri;
		}

		/// <summary>
		/// Gets the web URI.
		/// </summary>
		/// <remarks>
		/// The URI that the user should visit to resolve the issue.
		/// </remarks>
		/// <value>The web URI.</value>
		public Uri WebUri {
			get; private set;
		}
	}
}
