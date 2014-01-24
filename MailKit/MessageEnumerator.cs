//
// MessageEnumerator.cs
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

using System;
using System.Threading;
using System.Collections;
using System.Collections.Generic;

using MimeKit;

namespace MailKit {
	class MessageEnumerator : IEnumerator<MimeMessage>
	{
		readonly IFolder folder;
		MimeMessage message;
		int index = -1;

		/// <summary>
		/// Initializes a new instance of the <see cref="MailKit.MessageEnumerator"/> class.
		/// </summary>
		/// <param name="folder">The folder.</param>
		public MessageEnumerator (IFolder folder)
		{
			this.folder = folder;
		}

		#region IEnumerator<MimeMessage> implementation

		/// <summary>
		/// Gets the current message.
		/// </summary>
		/// <value>The current message.</value>
		public MimeMessage Current {
			get {
				if (index == -1)
					throw new InvalidOperationException ();

				return message;
			}
		}

		#endregion

		#region IEnumerator implementation

		bool IEnumerator.MoveNext ()
		{
			index++;

			if (index >= folder.Count)
				return false;

			try {
				message = folder.GetMessage (index, CancellationToken.None);
				return true;
			} catch {
				return false;
			}
		}

		void IEnumerator.Reset ()
		{
			index = -1;
		}

		object IEnumerator.Current {
			get { return Current; }
		}

		#endregion

		#region IDisposable implementation

		void IDisposable.Dispose ()
		{
		}

		#endregion
	}
}
