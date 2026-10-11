//
// ExpungeRequest.cs
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

using System.Collections.Generic;

namespace MailKit {
	/// <summary>
	/// A request for expunging messages from a folder.
	/// </summary>
	/// <remarks>
	/// <para>A request for expunging messages from a folder.</para>
	/// <para>This request is designed to be used with the <a href="Overload_MailKit_IMailFolder_Expunge.htm">Expunge</a> and
	/// <a href="Overload_MailKit_IMailFolder_ExpungeAsync.htm">ExpungeAsync</a> methods.</para>
	/// </remarks>
	public class ExpungeRequest : IExpungeRequest
	{
		/// <summary>
		/// Initializes a new instance of the <see cref="ExpungeRequest"/> class.
		/// </summary>
		/// <remarks>
		/// Creates a new <see cref="ExpungeRequest"/>.
		/// </remarks>
		public ExpungeRequest ()
		{
		}

		/// <summary>
		/// Called when a subset of the messages is about to be sent to the server.
		/// </summary>
		/// <remarks>
		/// <para>Called immediately before a command that will expunge the specified subset of messages is sent
		/// to the server.</para>
		/// <para>The default implementation does nothing.</para>
		/// </remarks>
		/// <param name="folder">The folder.</param>
		/// <param name="uids">The UIDs of the messages that are about to be expunged.</param>
		public virtual void OnStarted (IMailFolder folder, IList<UniqueId> uids)
		{
		}

		/// <summary>
		/// Called when the server has successfully expunged a subset of the messages.
		/// </summary>
		/// <remarks>
		/// <para>Called after the server has successfully completed the command that expunged the specified subset of messages.</para>
		/// <para>The default implementation does nothing.</para>
		/// </remarks>
		/// <param name="folder">The folder.</param>
		/// <param name="uids">The UIDs of the messages that were expunged.</param>
		public virtual void OnCompleted (IMailFolder folder, IList<UniqueId> uids)
		{
		}

		/// <summary>
		/// Called when the server reports the progress of a command.
		/// </summary>
		/// <remarks>
		/// <para>Called when the server reports the progress of the command that is expunging the subset of messages
		/// most recently passed to <c>OnStarted</c>. Progress is reported per command, so if the operation is split
		/// into multiple commands, the progress will start over for each subset of messages.</para>
		/// <para>The values are reported as-is from the server. The <paramref name="processed"/> value may decrease or the
		/// <paramref name="total"/> may change if the server discovers additional work. If the server is unable to count
		/// items, then it may report progress as a percentage, in which case <paramref name="total"/> will be <c>100</c>.
		/// If both values are <see langword="null" />, then the server has only indicated that the command is still in progress.</para>
		/// <note type="note">Progress is only reported by IMAP servers that support the
		/// <a href="https://tools.ietf.org/html/rfc9585">INPROGRESS</a> extension.</note>
		/// <para>The default implementation does nothing.</para>
		/// </remarks>
		/// <param name="folder">The folder that the messages belong to.</param>
		/// <param name="processed">The number of items processed so far, or <see langword="null" /> if unknown.</param>
		/// <param name="total">The total number of items to process, or <see langword="null" /> if unknown.</param>
		public virtual void OnProgress (IMailFolder folder, long? processed, long? total)
		{
		}
	}
}