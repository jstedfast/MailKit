//
// MessagesVanishedEventArgs.cs
//
// Author: Jeffrey Stedfast <jestedfa@microsoft.com>
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
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace MailKit {
	/// <summary>
	/// Event args used when a message vanishes from a folder.
	/// </summary>
	/// <remarks>
	/// Event args used when a message vanishes from a folder.
	/// </remarks>
	public class MessagesVanishedEventArgs : EventArgs
	{
		/// <summary>
		/// Initializes a new instance of the <see cref="MailKit.MessagesVanishedEventArgs"/> class.
		/// </summary>
		/// <remarks>
		/// Creates a new <see cref="MessagesVanishedEventArgs"/>.
		/// </remarks>
		/// <param name="uids">The list of unique identifiers.</param>
		/// <param name="earlier">If set to <c>true</c>, the messages vanished in the past as opposed to just now.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <paramref name="uids"/> is <c>null</c>.
		/// </exception>
		public MessagesVanishedEventArgs (IList<UniqueId> uids, bool earlier)
		{
			UniqueIds = new ReadOnlyCollection<UniqueId> (uids);
			Earlier = earlier;
		}

		/// <summary>
		/// Gets the unique identifiers of the messages that vanished.
		/// </summary>
		/// <remarks>
		/// Gets the unique identifiers of the messages that vanished.
		/// </remarks>
		/// <value>The unique identifiers.</value>
		public IList<UniqueId> UniqueIds {
			get; private set;
		}

		/// <summary>
		/// Gets whether the messages vanished inthe past as opposed to just now.
		/// </summary>
		/// <remarks>
		/// Gets whether the messages vanished inthe past as opposed to just now.
		/// </remarks>
		/// <value><c>true</c> if the messages vanished earlier; otherwise, <c>false</c>.</value>
		public bool Earlier {
			get; private set;
		}
	}
}
