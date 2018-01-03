//
// MessageEventArgs.cs
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
	/// Event args used when the state of a message changes.
	/// </summary>
	/// <remarks>
	/// Event args used when the state of a message changes.
	/// </remarks>
	public class MessageEventArgs : EventArgs
	{
		/// <summary>
		/// Initializes a new instance of the <see cref="T:MailKit.MessageEventArgs"/> class.
		/// </summary>
		/// <remarks>
		/// Creates a new <see cref="MessageEventArgs"/>.
		/// </remarks>
		/// <param name="index">The message index.</param>
		/// <exception cref="System.ArgumentOutOfRangeException">
		/// <paramref name="index"/> is out of range.
		/// </exception>
		public MessageEventArgs (int index)
		{
			if (index < 0)
				throw new ArgumentOutOfRangeException (nameof (index));

			Index = index;
		}

		/// <summary>
		/// Gets the index of the message that changed.
		/// </summary>
		/// <remarks>
		/// Gets the index of the message that changed.
		/// </remarks>
		/// <value>The index of the message.</value>
		public int Index {
			get; private set;
		}
	}
}
