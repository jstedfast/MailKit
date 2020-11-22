//
// IFetchRequest.cs
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

namespace MailKit {
	/// <summary>
	/// A request for fetching various properties of a message.
	/// </summary>
	/// <remarks>
	/// A request for fetching various properties of a message.
	/// </remarks>
	public interface IFetchRequest
	{
		/// <summary>
		/// Get or set the mod-sequence value that indicates the last known state of the messages being requested.
		/// </summary>
		/// <remarks>
		/// <para>Gets or sets the mod-sequence value that indicates the last known state of the messages being requested.</para>
		/// <para>If this property is set, the results returned by <a href="Overload_MailKit_IMailFolder_Fetch.htm">Fetch</a>
		/// or <a href="Overload_MailKit_IMailFolder_FetchAsync.htm">FetchAsync</a> will only include the message summaries which
		/// have a higher mod-sequence value than the one specified.</para>
		/// <para>If the mail store supports quick resynchronization and the application has enabled this feature via
		/// <see cref="IMailStore.EnableQuickResync(System.Threading.CancellationToken)"/>, then the Fetch or FetchAsync method
		/// will emit <see cref="IMailFolder.MessagesVanished"/> events for messages that were expunged from the folder after
		/// the change specified by the mod-sequence value.</para>
		/// <para>It should be noted that if another client has modified any message in the folder, the mail service may choose
		/// to return information that was not explicitly requested. It is therefore important to be prepared to handle both
		/// additional fields on a <see cref="IMessageSummary"/> for messages that were requested as well as summaries for
		/// messages that were not requested at all.</para>
		/// </remarks>
		ulong? ChangedSince { get; set; }

		/// <summary>
		/// Get or set the message summary items to fetch.
		/// </summary>
		/// <remarks>
		/// Gets or sets the message summary items to fetch.
		/// </remarks>
		MessageSummaryItems Items { get; set; }

		/// <summary>
		/// Get the set of headers that will be fetched.
		/// </summary>
		/// <remarks>
		/// Gets the set of headers that will be fetched.
		/// </remarks>
		/// <value>The set of headers to be fetched.</value>
		HeaderSet Headers { get; }
	}
}
