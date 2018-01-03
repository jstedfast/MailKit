//
// SmtpProtocolException.cs
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

namespace MailKit.Net.Smtp {
	/// <summary>
	/// An SMTP protocol exception.
	/// </summary>
	/// <remarks>
	/// The exception that is thrown when there is an error communicating with an SMTP server. An
	/// <see cref="SmtpProtocolException"/> is typically fatal and requires the <see cref="SmtpClient"/>
	/// to be reconnected.
	/// </remarks>
	/// <example>
	/// <code language="c#" source="Examples\SmtpExamples.cs" region="ExceptionHandling"/>
	/// </example>
#if SERIALIZABLE
	[Serializable]
#endif
	public class SmtpProtocolException : ProtocolException
	{
#if SERIALIZABLE
		/// <summary>
		/// Initializes a new instance of the <see cref="MailKit.Net.Smtp.SmtpProtocolException"/> class.
		/// </summary>
		/// <remarks>
		/// Creates a new <see cref="SmtpProtocolException"/> from the serialized data.
		/// </remarks>
		/// <param name="info">The serialization info.</param>
		/// <param name="context">The streaming context.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <paramref name="info"/> is <c>null</c>.
		/// </exception>
		[SecuritySafeCritical]
		protected SmtpProtocolException (SerializationInfo info, StreamingContext context) : base (info, context)
		{
		}
#endif

		/// <summary>
		/// Initializes a new instance of the <see cref="MailKit.Net.Smtp.SmtpProtocolException"/> class.
		/// </summary>
		/// <remarks>
		/// Creates a new <see cref="SmtpProtocolException"/>.
		/// </remarks>
		/// <param name="message">The error message.</param>
		/// <param name="innerException">The inner exception.</param>
		public SmtpProtocolException (string message, Exception innerException) : base (message, innerException)
		{
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="MailKit.Net.Smtp.SmtpProtocolException"/> class.
		/// </summary>
		/// <remarks>
		/// Creates a new <see cref="SmtpProtocolException"/>.
		/// </remarks>
		/// <param name="message">The error message.</param>
		public SmtpProtocolException (string message) : base (message)
		{
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="MailKit.Net.Smtp.SmtpProtocolException"/> class.
		/// </summary>
		/// <remarks>
		/// Creates a new <see cref="SmtpProtocolException"/>.
		/// </remarks>
		public SmtpProtocolException ()
		{
		}
	}
}
