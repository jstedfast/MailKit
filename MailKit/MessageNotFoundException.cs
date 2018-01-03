//
// MessageNotFoundException.cs
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
#if SERIALIZABLE
using System.Security;
using System.Runtime.Serialization;
#endif

namespace MailKit {
	/// <summary>
	/// The exception that is thrown when a message (or body part) could not be found.
	/// </summary>
	/// <remarks>
	/// This exception is thrown by methods such as
	/// <a href="Overload_MailKit_IMailFolder_GetMessage.htm">IMailFolder.GetMessage</a>,
	/// <a href="Overload_MailKit_IMailFolder_GetBodyPart.htm">IMailFolder.GetBodyPart</a>, or
	/// <a href="Overload_MailKit_IMailFolder_GetStream.htm">IMailFolder.GetStream</a>
	/// when the server's response does not contain the message, body part, or stream data requested.
	/// </remarks>
#if SERIALIZABLE
	[Serializable]
#endif
	public class MessageNotFoundException : Exception
	{
#if SERIALIZABLE
		/// <summary>
		/// Initializes a new instance of the <see cref="MailKit.MessageNotFoundException"/> class.
		/// </summary>
		/// <remarks>
		/// Deserializes a <see cref="MessageNotFoundException"/>.
		/// </remarks>
		/// <param name="info">The serialization info.</param>
		/// <param name="context">The streaming context.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <paramref name="info"/> is <c>null</c>.
		/// </exception>
		[SecuritySafeCritical]
		protected MessageNotFoundException (SerializationInfo info, StreamingContext context) : base (info, context)
		{
		}
#endif

		/// <summary>
		/// Initializes a new instance of the <see cref="MailKit.MessageNotFoundException"/> class.
		/// </summary>
		/// <remarks>
		/// Creates a new <see cref="MessageNotFoundException"/>.
		/// </remarks>
		/// <param name="message">The error message.</param>
		/// <param name="innerException">The inner exception.</param>
		/// <exception cref="System.ArgumentNullException">
		/// </exception>
		public MessageNotFoundException (string message, Exception innerException) : base (message, innerException)
		{
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="MailKit.MessageNotFoundException"/> class.
		/// </summary>
		/// <remarks>
		/// Creates a new <see cref="MessageNotFoundException"/>.
		/// </remarks>
		/// <param name="message">The error message.</param>
		public MessageNotFoundException (string message) : base (message)
		{
		}
	}
}
