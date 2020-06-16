//
// ImapCallbacks.cs
//
// Author: Jeffrey Stedfast <jestedfa@microsoft.com>
//
// Copyright (c) 2013-2020 .NET Foundation and Contributors
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

using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace MailKit.Net.Imap
{
	/// <summary>
	/// A callback used when fetching message streams.
	/// </summary>
	/// <remarks>
	/// <para>This callback is meant to be used with the various
	/// <a href="Overload_MailKit_Net_Imap_ImapFolder_GetStreams.htm">GetStreams</a>
	/// methods.</para>
	/// <para>Once this callback returns, the stream argument will be disposed, so
	/// it is important to consume the stream right away and not add it to a queue
	/// for later processing.</para>
	/// </remarks>
	/// <param name="folder">The IMAP folder that the message belongs to.</param>
	/// <param name="index">The index of the message in the folder.</param>
	/// <param name="uid">The UID of the message in the folder.</param>
	/// <param name="stream">The raw message (or part) stream.</param>
	public delegate void ImapFetchStreamCallback (ImapFolder folder, int index, UniqueId uid, Stream stream);

	/// <summary>
	/// An asynchronous callback used when fetching message streams.
	/// </summary>
	/// <remarks>
	/// <para>This callback is meant to be used with the various
	/// <a href="Overload_MailKit_Net_Imap_ImapFolder_GetStreamsAsync.htm">GetStreamsAsync</a>
	/// methods.</para>
	/// <para>Once this callback returns, the stream argument will be disposed, so
	/// it is important to consume the stream right away and not add it to a queue
	/// for later processing.</para>
	/// </remarks>
	/// <returns>An awaitable task context.</returns>
	/// <param name="folder">The IMAP folder that the message belongs to.</param>
	/// <param name="index">The index of the message in the folder.</param>
	/// <param name="uid">The UID of the message in the folder.</param>
	/// <param name="stream">The raw message (or part) stream.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	public delegate Task ImapFetchStreamAsyncCallback (ImapFolder folder, int index, UniqueId uid, Stream stream, CancellationToken cancellationToken);
}
