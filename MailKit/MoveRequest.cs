//
// MoveRequest.cs
//
// Author: Jeffrey Stedfast <jestedfa@microsoft.com>
//
// Copyright (c) 2013-2026 .NET Foundation and Contributors
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

namespace MailKit {
	/// <summary>
	/// A request for moving messages to another folder.
	/// </summary>
	/// <remarks>
	/// <para>A request for moving messages to another folder.</para>
	/// <para>This request is designed to be used with the <a href="Overload_MailKit_IMailFolder_MoveTo.htm">MoveTo</a> and
	/// <a href="Overload_MailKit_IMailFolder_MoveToAsync.htm">MoveToAsync</a> methods.</para>
	/// </remarks>
	public class MoveRequest : IMoveRequest
	{
		/// <summary>
		/// Initializes a new instance of the <see cref="MoveRequest"/> class.
		/// </summary>
		/// <remarks>
		/// Creates a new <see cref="MoveRequest"/>.
		/// </remarks>
		/// <param name="destination">The destination folder.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <paramref name="destination"/> is <see langword="null" />.
		/// </exception>
		public MoveRequest (IMailFolder destination)
		{
			if (destination == null)
				throw new ArgumentNullException (nameof (destination));

			Destination = destination;
		}

		/// <summary>
		/// Get the destination folder.
		/// </summary>
		/// <remarks>
		/// Gets the folder that the messages will be moved to.
		/// </remarks>
		/// <value>The destination folder.</value>
		public IMailFolder Destination {
			get; private set;
		}

		/// <summary>
		/// Called when a subset of the messages is about to be sent to the server.
		/// </summary>
		/// <remarks>
		/// <para>Called immediately before a command that will move the specified subset of messages is sent
		/// to the server.</para>
		/// <para>The default implementation does nothing.</para>
		/// </remarks>
		/// <param name="folder">The source folder.</param>
		/// <param name="uids">The UIDs of the messages that are about to be moved.</param>
		public virtual void OnStarted (IMailFolder folder, IList<UniqueId> uids)
		{
		}

		/// <summary>
		/// Called when a subset of the messages is about to be sent to the server.
		/// </summary>
		/// <remarks>
		/// <para>Called immediately before a command that will move the specified subset of messages is sent
		/// to the server.</para>
		/// <para>The default implementation does nothing.</para>
		/// </remarks>
		/// <param name="folder">The source folder.</param>
		/// <param name="indexes">The indexes of the messages that are about to be moved.</param>
		public virtual void OnStarted (IMailFolder folder, IList<int> indexes)
		{
		}

		/// <summary>
		/// Called when the server has successfully moved a subset of the messages.
		/// </summary>
		/// <remarks>
		/// <para>Called after the server has successfully completed the command that moved the specified subset of messages.</para>
		/// <para>The default implementation does nothing.</para>
		/// </remarks>
		/// <param name="folder">The source folder.</param>
		/// <param name="map">The mapping of the source UIDs to the UIDs of the messages in the destination folder.</param>
		public virtual void OnCompleted (IMailFolder folder, UniqueIdMap map)
		{
		}

		/// <summary>
		/// Called when the server has successfully moved a subset of the messages.
		/// </summary>
		/// <remarks>
		/// <para>Called after the server has successfully completed the command that moved the specified subset of messages.</para>
		/// <para>The default implementation does nothing.</para>
		/// </remarks>
		/// <param name="folder">The source folder.</param>
		/// <param name="indexes">The indexes of the messages that were moved.</param>
		public virtual void OnCompleted (IMailFolder folder, IList<int> indexes)
		{
		}
	}
}