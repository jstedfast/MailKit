//
// CopyRequest.cs
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
	/// A request for copying messages to another folder.
	/// </summary>
	/// <remarks>
	/// <para>A request for copying messages to another folder.</para>
	/// <para>This request is designed to be used with the <a href="Overload_MailKit_IMailFolder_CopyTo.htm">CopyTo</a> and
	/// <a href="Overload_MailKit_IMailFolder_CopyToAsync.htm">CopyToAsync</a> methods.</para>
	/// </remarks>
	public class CopyRequest : ICopyRequest
	{
		/// <summary>
		/// Initializes a new instance of the <see cref="CopyRequest"/> class.
		/// </summary>
		/// <remarks>
		/// Creates a new <see cref="CopyRequest"/>.
		/// </remarks>
		/// <param name="destination">The destination folder.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <paramref name="destination"/> is <see langword="null" />.
		/// </exception>
		public CopyRequest (IMailFolder destination)
		{
			if (destination == null)
				throw new ArgumentNullException (nameof (destination));

			Destination = destination;
		}

		/// <summary>
		/// Get the destination folder.
		/// </summary>
		/// <remarks>
		/// Gets the folder that the messages will be copied to.
		/// </remarks>
		/// <value>The destination folder.</value>
		public IMailFolder Destination {
			get; private set;
		}

		/// <summary>
		/// Called when a subset of the messages is about to be sent to the server.
		/// </summary>
		/// <remarks>
		/// <para>Called immediately before a command that will copy the specified subset of messages is sent
		/// to the server.</para>
		/// <para>The default implementation does nothing.</para>
		/// </remarks>
		/// <param name="folder">The source folder.</param>
		/// <param name="uids">The UIDs of the messages that are about to be copied.</param>
		public virtual void OnStarted (IMailFolder folder, IList<UniqueId> uids)
		{
		}

		/// <summary>
		/// Called when a subset of the messages is about to be sent to the server.
		/// </summary>
		/// <remarks>
		/// <para>Called immediately before a command that will copy the specified subset of messages is sent
		/// to the server.</para>
		/// <para>The default implementation does nothing.</para>
		/// </remarks>
		/// <param name="folder">The source folder.</param>
		/// <param name="indexes">The indexes of the messages that are about to be copied.</param>
		public virtual void OnStarted (IMailFolder folder, IList<int> indexes)
		{
		}

		/// <summary>
		/// Called when the server has successfully copied a subset of the messages.
		/// </summary>
		/// <remarks>
		/// <para>Called after the server has successfully completed the command that copied the specified subset of messages.</para>
		/// <para>The default implementation does nothing.</para>
		/// </remarks>
		/// <param name="folder">The source folder.</param>
		/// <param name="map">The mapping of the source UIDs to the UIDs of the messages in the destination folder.</param>
		public virtual void OnCompleted (IMailFolder folder, UniqueIdMap map)
		{
		}

		/// <summary>
		/// Called when the server has successfully copied a subset of the messages.
		/// </summary>
		/// <remarks>
		/// <para>Called after the server has successfully completed the command that copied the specified subset of messages.</para>
		/// <para>The default implementation does nothing.</para>
		/// </remarks>
		/// <param name="folder">The source folder.</param>
		/// <param name="indexes">The indexes of the messages that were copied.</param>
		public virtual void OnCompleted (IMailFolder folder, IList<int> indexes)
		{
		}
	}
}