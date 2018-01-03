//
// Pop3CommandException.cs
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

namespace MailKit.Net.Pop3 {
	/// <summary>
	/// A POP3 command exception.
	/// </summary>
	/// <remarks>
	/// The exception that is thrown when a POP3 command fails. Unlike a <see cref="Pop3ProtocolException"/>,
	/// a <see cref="Pop3CommandException"/> does not require the <see cref="Pop3Client"/> to be reconnected.
	/// </remarks>
	/// <example>
	/// <code language="c#" source="Examples\Pop3Examples.cs" region="ExceptionHandling"/>
	/// </example>
#if SERIALIZABLE
	[Serializable]
#endif
	public class Pop3CommandException : CommandException
	{
#if SERIALIZABLE
		/// <summary>
		/// Initializes a new instance of the <see cref="MailKit.Net.Pop3.Pop3CommandException"/> class.
		/// </summary>
		/// <remarks>
		/// Creates a new <see cref="Pop3CommandException"/> from the serialized data.
		/// </remarks>
		/// <param name="info">The serialization info.</param>
		/// <param name="context">The streaming context.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <paramref name="info"/> is <c>null</c>.
		/// </exception>
		[SecuritySafeCritical]
		protected Pop3CommandException (SerializationInfo info, StreamingContext context) : base (info, context)
		{
			StatusText = info.GetString ("StatusText");
		}
#endif

		/// <summary>
		/// Initializes a new instance of the <see cref="MailKit.Net.Pop3.Pop3CommandException"/> class.
		/// </summary>
		/// <remarks>
		/// Creates a new <see cref="Pop3CommandException"/>.
		/// </remarks>
		/// <param name="message">The error message.</param>
		/// <param name="innerException">An inner exception.</param>
		public Pop3CommandException (string message, Exception innerException) : base (message, innerException)
		{
			StatusText = string.Empty;
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="MailKit.Net.Pop3.Pop3CommandException"/> class.
		/// </summary>
		/// <remarks>
		/// Creates a new <see cref="Pop3CommandException"/>.
		/// </remarks>
		/// <param name="message">The error message.</param>
		/// <param name="statusText">The response status text.</param>
		/// <param name="innerException">An inner exception.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <paramref name="statusText"/> is <c>null</c>.
		/// </exception>
		public Pop3CommandException (string message, string statusText, Exception innerException) : base (message, innerException)
		{
			if (statusText == null)
				throw new ArgumentNullException (nameof (statusText));

			StatusText = statusText;
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="MailKit.Net.Pop3.Pop3CommandException"/> class.
		/// </summary>
		/// <remarks>
		/// Creates a new <see cref="Pop3CommandException"/>.
		/// </remarks>
		/// <param name="message">The error message.</param>
		public Pop3CommandException (string message) : base (message)
		{
			StatusText = string.Empty;
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="MailKit.Net.Pop3.Pop3CommandException"/> class.
		/// </summary>
		/// <remarks>
		/// Creates a new <see cref="Pop3CommandException"/>.
		/// </remarks>
		/// <param name="message">The error message.</param>
		/// <param name="statusText">The response status text.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <paramref name="statusText"/> is <c>null</c>.
		/// </exception>
		public Pop3CommandException (string message, string statusText) : base (message)
		{
			if (statusText == null)
				throw new ArgumentNullException (nameof (statusText));

			StatusText = statusText;
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="MailKit.Net.Pop3.Pop3CommandException"/> class.
		/// </summary>
		/// <remarks>
		/// Creates a new <see cref="Pop3CommandException"/>.
		/// </remarks>
		public Pop3CommandException ()
		{
			StatusText = string.Empty;
		}

		/// <summary>
		/// Get the response status text.
		/// </summary>
		/// <remarks>
		/// Gets the response status text.
		/// </remarks>
		/// <example>
		/// <code language="c#" source="Examples\Pop3Examples.cs" region="ExceptionHandling"/>
		/// </example>
		/// <value>The response status text.</value>
		public string StatusText {
			get; private set;
		}

#if SERIALIZABLE
		/// <summary>
		/// When overridden in a derived class, sets the <see cref="System.Runtime.Serialization.SerializationInfo"/>
		/// with information about the exception.
		/// </summary>
		/// <remarks>
		/// Serializes the state of the <see cref="FolderNotFoundException"/>.
		/// </remarks>
		/// <param name="info">The serialization info.</param>
		/// <param name="context">The streaming context.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <paramref name="info"/> is <c>null</c>.
		/// </exception>
		[SecurityCritical]
		public override void GetObjectData (SerializationInfo info, StreamingContext context)
		{
			base.GetObjectData (info, context);

			info.AddValue ("StatusText", StatusText);
		}
#endif
	}
}
