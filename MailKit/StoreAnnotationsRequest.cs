//
// StoreAnnotationsRequest.cs
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
	/// A request for storing annotations.
	/// </summary>
	/// <remarks>
	/// <para>A request suitable for storing annotations.</para>
	/// <para>This request is designed to be used with the <a href="Overload_MailKit_IMailFolder_Store.htm">Store</a> and
	/// <a href="Overload_MailKit_IMailFolder_StoreAsync.htm">StoreAsync</a> methods.</para>
	/// </remarks>
	public class StoreAnnotationsRequest : IStoreAnnotationsRequest
	{
		/// <summary>
		/// Initializes a new instance of the <see cref="StoreAnnotationsRequest"/> class.
		/// </summary>
		/// <remarks>
		/// Creates a new <see cref="StoreAnnotationsRequest"/>.
		/// </remarks>
		/// <param name="annotations">The annotations to store.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <paramref name="annotations"/> is <see langword="null" />.
		/// </exception>
		public StoreAnnotationsRequest (IList<Annotation> annotations)
		{
			if (annotations == null)
				throw new ArgumentNullException (nameof (annotations));

			Annotations = annotations;
		}

		/// <summary>
		/// Get the annotations to store.
		/// </summary>
		/// <remarks>
		/// Gets the annotations to store.
		/// </remarks>
		/// <value>The annotations.</value>
		public IList<Annotation> Annotations {
			get;
		}

		/// <summary>
		/// Get or set a mod-sequence number that the store operation should use to decide if the annotations of a message should be updated or not.
		/// </summary>
		/// <remarks>
		/// <para>Gets or sets a mod-sequence number that the store operation should use to decide if the annotations of a message should be updated or not.</para>
		/// <para>For each message specified in the message set, the server performs the following. If the mod-sequence of every metadata item of the
		/// message affected by the store operation is equal to or less than the specified <see cref="UnchangedSince"/> value, then the requested operation
		/// is performed.</para>
		/// <para>However, if the mod-sequence of any metadata item of the message is greater than the specified <see cref="UnchangedSince"/> value, then the
		/// requested operation WILL NOT be performed. In this case, the mod-sequence attribute of the message is not updated, and the message index
		/// (or unique identifier in cases where <see cref="IMailFolder.Store(IList{UniqueId}, IStoreAnnotationsRequest, System.Threading.CancellationToken)"/> or
		/// <see cref="IMailFolder.StoreAsync(IList{UniqueId}, IStoreAnnotationsRequest, System.Threading.CancellationToken)"/> is used) is added to the list of
		/// messages that failed the UNCHANGEDSINCE test.</para>
		/// <note type="note">The <see cref="UnchangedSince"/> mod-sequence number can only be used if the server supports the <see cref="FolderFeature.ModSequences"/>
		/// feature.</note>
		/// </remarks>
		/// <value>The mod-sequence number.</value>
		public ulong? UnchangedSince {
			get; set;
		}
		/// <summary>
		/// Called when a subset of the messages is about to be sent to the server.
		/// </summary>
		/// <remarks>
		/// <para>Called immediately before a command that will update the specified subset of messages is sent
		/// to the server.</para>
		/// <para>The default implementation does nothing.</para>
		/// </remarks>
		/// <param name="folder">The folder that the messages belong to.</param>
		/// <param name="uids">The UIDs of the messages that are about to be updated.</param>
		public virtual void OnStarted (IMailFolder folder, IList<UniqueId> uids)
		{
		}

		/// <summary>
		/// Called when a subset of the messages is about to be sent to the server.
		/// </summary>
		/// <remarks>
		/// <para>Called immediately before a command that will update the specified subset of messages is sent
		/// to the server.</para>
		/// <para>The default implementation does nothing.</para>
		/// </remarks>
		/// <param name="folder">The folder that the messages belong to.</param>
		/// <param name="indexes">The indexes of the messages that are about to be updated.</param>
		public virtual void OnStarted (IMailFolder folder, IList<int> indexes)
		{
		}

		/// <summary>
		/// Called when the server has successfully updated a subset of the messages.
		/// </summary>
		/// <remarks>
		/// <para>Called after the server has successfully completed the command that updated the specified subset of messages.</para>
		/// <para>The default implementation does nothing.</para>
		/// </remarks>
		/// <param name="folder">The folder that the messages belong to.</param>
		/// <param name="uids">The UIDs of the messages that were sent to the server.</param>
		/// <param name="unmodified">The UIDs of the messages that were not updated because they were modified since
		/// <see cref="UnchangedSince"/>.</param>
		public virtual void OnCompleted (IMailFolder folder, IList<UniqueId> uids, IList<UniqueId> unmodified)
		{
		}

		/// <summary>
		/// Called when the server has successfully updated a subset of the messages.
		/// </summary>
		/// <remarks>
		/// <para>Called after the server has successfully completed the command that updated the specified subset of messages.</para>
		/// <para>The default implementation does nothing.</para>
		/// </remarks>
		/// <param name="folder">The folder that the messages belong to.</param>
		/// <param name="indexes">The indexes of the messages that were sent to the server.</param>
		/// <param name="unmodified">The indexes of the messages that were not updated because they were modified since
		/// <see cref="UnchangedSince"/>.</param>
		public virtual void OnCompleted (IMailFolder folder, IList<int> indexes, IList<int> unmodified)
		{
		}
	}
}
