//
// SmtpResponse.cs
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

namespace MailKit.Net.Smtp {
	/// <summary>
	/// An SMTP command response.
	/// </summary>
	/// <remarks>
	/// An SMTP command response.
	/// </remarks>
	public class SmtpResponse
	{
		/// <summary>
		/// Get the status code.
		/// </summary>
		/// <remarks>
		/// Gets the status code.
		/// </remarks>
		/// <value>The status code.</value>
		public SmtpStatusCode StatusCode { get; private set; }

		/// <summary>
		/// Get the response text.
		/// </summary>
		/// <remarks>
		/// Gets the response text.
		/// </remarks>
		/// <value>The response text.</value>
		public string Response { get; private set; }

		/// <summary>
		/// Initializes a new instance of the <see cref="MailKit.Net.Smtp.SmtpResponse"/> class.
		/// </summary>
		/// <remarks>
		/// Creates a new <see cref="SmtpResponse"/>.
		/// </remarks>
		/// <param name="code">The status code.</param>
		/// <param name="response">The response text.</param>
		public SmtpResponse (SmtpStatusCode code, string response)
		{
			StatusCode = code;
			Response = response;
		}
	}
}
