//
// IMessageSpool.cs
//
// Author: Jeffrey Stedfast <jeff@xamarin.com>
//
// Copyright (c) 2013 Jeffrey Stedfast
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
using System.Collections.Generic;

using MimeKit;

namespace MailKit {
	public interface IMessageSpool : IMessageService
	{
		bool SupportsUids { get; }

		int Count (CancellationToken token);

		string GetMessageUid (int index, CancellationToken token);
		string[] GetMessageUids (CancellationToken token);

		int GetMessageSize (string uid, CancellationToken token);
		int GetMessageSize (int index, CancellationToken token);
		int[] GetMessageSizes (CancellationToken token);

		HeaderList GetMessageHeaders (string uid, CancellationToken token);
		HeaderList GetMessageHeaders (int index, CancellationToken token);

		MimeMessage GetMessage (string uid, CancellationToken token);
		MimeMessage GetMessage (int index, CancellationToken token);

		void DeleteMessage (string uid, CancellationToken token);
		void DeleteMessage (int index, CancellationToken token);

		void Reset (CancellationToken token);
		void NoOp (CancellationToken token);
	}
}
