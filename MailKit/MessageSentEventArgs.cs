//
// MessageSentEventArgs.cs
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

using MimeKit;

namespace MailKit {
	/// <summary>
	/// Event args used when a message is successfully sent.
	/// </summary>
	/// <remarks>
	/// Event args used when message is successfully sent.
	/// </remarks>
	public class MessageSentEventArgs : EventArgs
	{
		/// <summary>
		/// Initializes a new instance of the <see cref="MailKit.MessageSentEventArgs"/> class.
		/// </summary>
		/// <remarks>
		/// Creates a new <see cref="MessageSentEventArgs"/>.
		/// </remarks>
		/// <param name="message">The message that was just sent.</param>
		/// <param name="response">The response from the server.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <para><paramref name="message"/> is <c>null</c>.</para>
		/// <para>-or-</para>
		/// <para><paramref name="response"/> is <c>null</c>.</para>
		/// </exception>
		public MessageSentEventArgs (MimeMessage message, string response)
		{
			if (message == null)
				throw new ArgumentNullException (nameof (message));

			if (response == null)
				throw new ArgumentNullException (nameof (response));

			Message = message;
			Response = response;
		}

		/// <summary>
		/// Get the message that was just sent.
		/// </summary>
		/// <remarks>
		/// Gets the message that was just sent.
		/// </remarks>
		/// <value>The message.</value>
		public MimeMessage Message {
			get; private set;
		}

		/// <summary>
		/// Get the server's response.
		/// </summary>
		/// <remarks>
		/// Gets the server's response.
		/// </remarks>
		/// <value>The response.</value>
		public string Response {
			get; private set;
		}
	}
}
