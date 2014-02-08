//
// MessagesVanishedEventArgs.cs
//
// Author: Jeffrey Stedfast <jeff@xamarin.com>
//
// Copyright (c) 2014 Jeffrey Stedfast
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
	/// Event args used when a message vanishes from a folder.
	/// </summary>
	public class MessagesVanishedEventArgs : EventArgs
	{
		internal MessagesVanishedEventArgs (UniqueId[] uids, bool earlier)
		{
			Earlier = earlier;
			UniqueIds = uids;
		}

		/// <summary>
		/// Gets the unique identifiers of the messages that vanished.
		/// </summary>
		/// <value>The unique identifiers.</value>
		public UniqueId[] UniqueIds {
			get; private set;
		}

		/// <summary>
		/// Gets whether the messages vanished inthe past as opposed to just now.
		/// </summary>
		/// <value><c>true</c> if the messages vanished earlier; otherwise, <c>false</c>.</value>
		public bool Earlier {
			get; private set;
		}
	}
}
