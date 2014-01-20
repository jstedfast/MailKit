//
// ImapCommandException.cs
//
// Author: Jeffrey Stedfast <jeff@xamarin.com>
//
// Copyright (c) 2014 Xamarin Inc.
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

namespace MailKit.Net.Imap {
	/// <summary>
	/// An exception that is thrown when an IMAP command returns NO or BAD.
	/// </summary>
	/// <remarks>
	/// This exception can be thrown by most of the methods in <see cref="ImapFolder"/>.
	/// </remarks>
	public class ImapCommandException : ProtocolException
	{
		/// <summary>
		/// Initializes a new instance of the <see cref="MailKit.Net.Imap.ImapCommandException"/> class.
		/// </summary>
		/// <param name="command">The command.</param>
		/// <param name="result">The command result.</param>
		internal ImapCommandException (string command, ImapCommandResult result)
			: base (string.Format ("The IMAP server replied to the '{0}' command with a '{1}' response.", command, result.ToString ().ToUpperInvariant ()))
		{
		}
	}
}
