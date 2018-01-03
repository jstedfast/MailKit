//
// IMailSpool.cs
//
// Author: Jeffrey Stedfast <jestedfa@microsoft.com>
//
// Copyright (c) 2013-2018 Xamarin Inc. (www.xamarin.com)
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
using System.Collections.Generic;

using MimeKit;

namespace MailKit {
	/// <summary>
	/// An interface for retreiving messages from a spool.
	/// </summary>
	/// <remarks>
	/// An interface for retreiving messages from a spool.
	/// </remarks>
	public interface IMailSpool : IMailService, IEnumerable<MimeMessage>
	{
		/// <summary>
		/// Get the number of messages available in the message spool.
		/// </summary>
		/// <remarks>
		/// <para>Gets the number of messages available in the message spool.</para>
		/// <para>Once authenticated, the <see cref="Count"/> property will be set
		/// to the number of available messages in the spool.</para>
		/// </remarks>
		/// <value>The message count.</value>
		int Count { get; }

		/// <summary>
		/// Get whether or not the service supports referencing messages by UIDs.
		/// </summary>
		/// <remarks>
		/// <para>Not all servers support referencing messages by UID, so this property should
		/// be checked before using <see cref="GetMessageUid(int, CancellationToken)"/>
		/// and <see cref="GetMessageUids(CancellationToken)"/>.</para>
		/// <para>If the server does not support UIDs, then all methods that take UID arguments
		/// along with <see cref="GetMessageUid(int, CancellationToken)"/> and
		/// <see cref="GetMessageUids(CancellationToken)"/> will fail.</para>
		/// </remarks>
		/// <value><c>true</c> if supports uids; otherwise, <c>false</c>.</value>
		bool SupportsUids { get; }

		/// <summary>
		/// Get the UID of the message at the specified index.
		/// </summary>
		/// <remarks>
		/// Not all servers support UIDs, so you should first check
		/// the <see cref="SupportsUids"/> property.
		/// </remarks>
		/// <returns>The message UID.</returns>
		/// <param name="index">The message index.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		string GetMessageUid (int index, CancellationToken cancellationToken = default (CancellationToken));

		/// <summary>
		/// Asynchronously get the UID of the message at the specified index.
		/// </summary>
		/// <remarks>
		/// Not all servers support UIDs, so you should first check
		/// the <see cref="SupportsUids"/> property.
		/// </remarks>
		/// <returns>The message UID.</returns>
		/// <param name="index">The message index.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		Task<string> GetMessageUidAsync (int index, CancellationToken cancellationToken = default (CancellationToken));

		/// <summary>
		/// Get the full list of available message UIDs.
		/// </summary>
		/// <remarks>
		/// Not all servers support UIDs, so you should first check
		/// the <see cref="SupportsUids"/> property.
		/// </remarks>
		/// <returns>The message UIDs.</returns>
		/// <param name="cancellationToken">The cancellation token.</param>
		IList<string> GetMessageUids (CancellationToken cancellationToken = default (CancellationToken));

		/// <summary>
		/// Asynchronously get the full list of available message UIDs.
		/// </summary>
		/// <remarks>
		/// Not all servers support UIDs, so you should first check
		/// the <see cref="SupportsUids"/> property.
		/// </remarks>
		/// <returns>The message UIDs.</returns>
		/// <param name="cancellationToken">The cancellation token.</param>
		Task<IList<string>> GetMessageUidsAsync (CancellationToken cancellationToken = default (CancellationToken));

		/// <summary>
		/// Get the size of the specified message, in bytes.
		/// </summary>
		/// <remarks>
		/// Gets the size of the specified message, in bytes.
		/// </remarks>
		/// <returns>The message size, in bytes.</returns>
		/// <param name="index">The index of the message.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		int GetMessageSize (int index, CancellationToken cancellationToken = default (CancellationToken));

		/// <summary>
		/// Asynchronously get the size of the specified message, in bytes.
		/// </summary>
		/// <remarks>
		/// Asynchronously gets the size of the specified message, in bytes.
		/// </remarks>
		/// <returns>The message size, in bytes.</returns>
		/// <param name="index">The index of the message.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		Task<int> GetMessageSizeAsync (int index, CancellationToken cancellationToken = default (CancellationToken));

		/// <summary>
		/// Get the sizes for all available messages, in bytes.
		/// </summary>
		/// <remarks>
		/// Gets the sizes for all available messages, in bytes.
		/// </remarks>
		/// <returns>The message sizes, in bytes.</returns>
		/// <param name="cancellationToken">The cancellation token.</param>
		IList<int> GetMessageSizes (CancellationToken cancellationToken = default (CancellationToken));

		/// <summary>
		/// Asynchronously get the sizes for all available messages, in bytes.
		/// </summary>
		/// <remarks>
		/// Asynchronously gets the sizes for all available messages, in bytes.
		/// </remarks>
		/// <returns>The message sizes, in bytes.</returns>
		/// <param name="cancellationToken">The cancellation token.</param>
		Task<IList<int>> GetMessageSizesAsync (CancellationToken cancellationToken = default (CancellationToken));

		/// <summary>
		/// Get the headers for the specified message.
		/// </summary>
		/// <remarks>
		/// Gets the headers for the specified message.
		/// </remarks>
		/// <returns>The message headers.</returns>
		/// <param name="index">The index of the message.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		HeaderList GetMessageHeaders (int index, CancellationToken cancellationToken = default (CancellationToken));

		/// <summary>
		/// Asynchronously get the headers for the specified message.
		/// </summary>
		/// <remarks>
		/// Asynchronously gets the headers for the specified message.
		/// </remarks>
		/// <returns>The message headers.</returns>
		/// <param name="index">The index of the message.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		Task<HeaderList> GetMessageHeadersAsync (int index, CancellationToken cancellationToken = default (CancellationToken));

		/// <summary>
		/// Get the headers for the specified messages.
		/// </summary>
		/// <remarks>
		/// Gets the headers for the specified messages.
		/// </remarks>
		/// <returns>The headers for the specified messages.</returns>
		/// <param name="indexes">The indexes of the messages.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		IList<HeaderList> GetMessageHeaders (IList<int> indexes, CancellationToken cancellationToken = default (CancellationToken));

		/// <summary>
		/// Asynchronously get the headers for the specified messages.
		/// </summary>
		/// <remarks>
		/// Asynchronously gets the headers for the specified messages.
		/// </remarks>
		/// <returns>The headers for the specified messages.</returns>
		/// <param name="indexes">The indexes of the messages.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		Task<IList<HeaderList>> GetMessageHeadersAsync (IList<int> indexes, CancellationToken cancellationToken = default (CancellationToken));

		/// <summary>
		/// Get the headers of the messages within the specified range.
		/// </summary>
		/// <remarks>
		/// Gets the headers of the messages within the specified range.
		/// </remarks>
		/// <returns>The headers of the messages within the specified range.</returns>
		/// <param name="startIndex">The index of the first message to get.</param>
		/// <param name="count">The number of messages to get.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		IList<HeaderList> GetMessageHeaders (int startIndex, int count, CancellationToken cancellationToken = default (CancellationToken));

		/// <summary>
		/// Get the headers of the messages within the specified range.
		/// </summary>
		/// <remarks>
		/// Gets the headers of the messages within the specified range.
		/// </remarks>
		/// <returns>The headers of the messages within the specified range.</returns>
		/// <param name="startIndex">The index of the first message to get.</param>
		/// <param name="count">The number of messages to get.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		Task<IList<HeaderList>> GetMessageHeadersAsync (int startIndex, int count, CancellationToken cancellationToken = default (CancellationToken));

		/// <summary>
		/// Get the message at the specified index.
		/// </summary>
		/// <remarks>
		/// Gets the message at the specified index.
		/// </remarks>
		/// <returns>The message.</returns>
		/// <param name="index">The index of the message.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <param name="progress">The progress reporting mechanism.</param>
		MimeMessage GetMessage (int index, CancellationToken cancellationToken = default (CancellationToken), ITransferProgress progress = null);

		/// <summary>
		/// Asynchronously get the message at the specified index.
		/// </summary>
		/// <remarks>
		/// Asynchronously gets the message at the specified index.
		/// </remarks>
		/// <returns>The message.</returns>
		/// <param name="index">The index of the message.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <param name="progress">The progress reporting mechanism.</param>
		Task<MimeMessage> GetMessageAsync (int index, CancellationToken cancellationToken = default (CancellationToken), ITransferProgress progress = null);

		/// <summary>
		/// Get the messages at the specified indexes.
		/// </summary>
		/// <remarks>
		/// Gets the messages at the specified indexes.
		/// </remarks>
		/// <returns>The messages.</returns>
		/// <param name="indexes">The indexes of the messages.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <param name="progress">The progress reporting mechanism.</param>
		IList<MimeMessage> GetMessages (IList<int> indexes, CancellationToken cancellationToken = default (CancellationToken), ITransferProgress progress = null);

		/// <summary>
		/// Asynchronously get the messages at the specified indexes.
		/// </summary>
		/// <remarks>
		/// Asynchronously gets the messages at the specified indexes.
		/// </remarks>
		/// <returns>The messages.</returns>
		/// <param name="indexes">The indexes of the messages.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <param name="progress">The progress reporting mechanism.</param>
		Task<IList<MimeMessage>> GetMessagesAsync (IList<int> indexes, CancellationToken cancellationToken = default (CancellationToken), ITransferProgress progress = null);

		/// <summary>
		/// Get the messages within the specified range.
		/// </summary>
		/// <remarks>
		/// Gets the messages within the specified range.
		/// </remarks>
		/// <returns>The messages.</returns>
		/// <param name="startIndex">The index of the first message to get.</param>
		/// <param name="count">The number of messages to get.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <param name="progress">The progress reporting mechanism.</param>
		IList<MimeMessage> GetMessages (int startIndex, int count, CancellationToken cancellationToken = default (CancellationToken), ITransferProgress progress = null);

		/// <summary>
		/// Asynchronously get the messages within the specified range.
		/// </summary>
		/// <remarks>
		/// Asynchronously gets the messages within the specified range.
		/// </remarks>
		/// <returns>The messages.</returns>
		/// <param name="startIndex">The index of the first message to get.</param>
		/// <param name="count">The number of messages to get.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <param name="progress">The progress reporting mechanism.</param>
		Task<IList<MimeMessage>> GetMessagesAsync (int startIndex, int count, CancellationToken cancellationToken = default (CancellationToken), ITransferProgress progress = null);

		/// <summary>
		/// Get the message or header stream at the specified index.
		/// </summary>
		/// <remarks>
		/// Gets the message or header stream at the specified index.
		/// </remarks>
		/// <returns>The message or header stream.</returns>
		/// <param name="index">The index of the message.</param>
		/// <param name="headersOnly"><c>true</c> if only the headers should be retrieved; otherwise, <c>false</c>.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <param name="progress">The progress reporting mechanism.</param>
		Stream GetStream (int index, bool headersOnly = false, CancellationToken cancellationToken = default (CancellationToken), ITransferProgress progress = null);

		/// <summary>
		/// Asynchronously get the message or header stream at the specified index.
		/// </summary>
		/// <remarks>
		/// Asynchronously gets the message or header stream at the specified index.
		/// </remarks>
		/// <returns>The message or header stream.</returns>
		/// <param name="index">The index of the message.</param>
		/// <param name="headersOnly"><c>true</c> if only the headers should be retrieved; otherwise, <c>false</c>.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <param name="progress">The progress reporting mechanism.</param>
		Task<Stream> GetStreamAsync (int index, bool headersOnly = false, CancellationToken cancellationToken = default (CancellationToken), ITransferProgress progress = null);

		/// <summary>
		/// Get the message or header streams at the specified index.
		/// </summary>
		/// <remarks>
		/// Gets the message or header streams at the specified index.
		/// </remarks>
		/// <returns>The message or header streams.</returns>
		/// <param name="indexes">The indexes of the messages.</param>
		/// <param name="headersOnly"><c>true</c> if only the headers should be retrieved; otherwise, <c>false</c>.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <param name="progress">The progress reporting mechanism.</param>
		IList<Stream> GetStreams (IList<int> indexes, bool headersOnly = false, CancellationToken cancellationToken = default (CancellationToken), ITransferProgress progress = null);

		/// <summary>
		/// Asynchronously get the message or header streams at the specified indexes.
		/// </summary>
		/// <remarks>
		/// Asynchronously gets the message or header streams at the specified indexes.
		/// </remarks>
		/// <returns>The message or header streams.</returns>
		/// <param name="indexes">The indexes of the messages.</param>
		/// <param name="headersOnly"><c>true</c> if only the headers should be retrieved; otherwise, <c>false</c>.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <param name="progress">The progress reporting mechanism.</param>
		Task<IList<Stream>> GetStreamsAsync (IList<int> indexes, bool headersOnly = false, CancellationToken cancellationToken = default (CancellationToken), ITransferProgress progress = null);

		/// <summary>
		/// Get the message or header streams within the specified range.
		/// </summary>
		/// <remarks>
		/// Gets the message or header streams within the specified range.
		/// </remarks>
		/// <returns>The message or header streams.</returns>
		/// <param name="startIndex">The index of the first stream to get.</param>
		/// <param name="count">The number of streams to get.</param>
		/// <param name="headersOnly"><c>true</c> if only the headers should be retrieved; otherwise, <c>false</c>.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <param name="progress">The progress reporting mechanism.</param>
		IList<Stream> GetStreams (int startIndex, int count, bool headersOnly = false, CancellationToken cancellationToken = default (CancellationToken), ITransferProgress progress = null);

		/// <summary>
		/// Asynchronously get the message or header streams within the specified range.
		/// </summary>
		/// <remarks>
		/// Asynchronously gets the message or header streams within the specified range.
		/// </remarks>
		/// <returns>The messages.</returns>
		/// <param name="startIndex">The index of the first stream to get.</param>
		/// <param name="count">The number of streams to get.</param>
		/// <param name="headersOnly"><c>true</c> if only the headers should be retrieved; otherwise, <c>false</c>.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <param name="progress">The progress reporting mechanism.</param>
		Task<IList<Stream>> GetStreamsAsync (int startIndex, int count, bool headersOnly = false, CancellationToken cancellationToken = default (CancellationToken), ITransferProgress progress = null);

		/// <summary>
		/// Mark the specified message for deletion.
		/// </summary>
		/// <remarks>
		/// Messages marked for deletion are not actually deleted until the session
		/// is cleanly disconnected
		/// (see <see cref="IMailService.Disconnect(bool, CancellationToken)"/>).
		/// </remarks>
		/// <param name="index">The index of the message.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		void DeleteMessage (int index, CancellationToken cancellationToken = default (CancellationToken));

		/// <summary>
		/// Asynchronously mark the specified message for deletion.
		/// </summary>
		/// <remarks>
		/// Messages marked for deletion are not actually deleted until the session
		/// is cleanly disconnected
		/// (see <see cref="IMailService.Disconnect(bool, CancellationToken)"/>).
		/// </remarks>
		/// <returns>An asynchronous task context.</returns>
		/// <param name="index">The index of the message.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		Task DeleteMessageAsync (int index, CancellationToken cancellationToken = default (CancellationToken));

		/// <summary>
		/// Mark the specified messages for deletion.
		/// </summary>
		/// <remarks>
		/// Messages marked for deletion are not actually deleted until the session
		/// is cleanly disconnected
		/// (see <see cref="IMailService.Disconnect(bool, CancellationToken)"/>).
		/// </remarks>
		/// <param name="indexes">The indexes of the messages.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		void DeleteMessages (IList<int> indexes, CancellationToken cancellationToken = default (CancellationToken));

		/// <summary>
		/// Asynchronously mark the specified messages for deletion.
		/// </summary>
		/// <remarks>
		/// Messages marked for deletion are not actually deleted until the session
		/// is cleanly disconnected
		/// (see <see cref="IMailService.Disconnect(bool, CancellationToken)"/>).
		/// </remarks>
		/// <returns>An asynchronous task context.</returns>
		/// <param name="indexes">The indexes of the messages.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		Task DeleteMessagesAsync (IList<int> indexes, CancellationToken cancellationToken = default (CancellationToken));

		/// <summary>
		/// Mark the specified range of messages for deletion.
		/// </summary>
		/// <remarks>
		/// Messages marked for deletion are not actually deleted until the session
		/// is cleanly disconnected
		/// (see <see cref="IMailService.Disconnect(bool, CancellationToken)"/>).
		/// </remarks>
		/// <param name="startIndex">The index of the first message to mark for deletion.</param>
		/// <param name="count">The number of messages to mark for deletion.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		void DeleteMessages (int startIndex, int count, CancellationToken cancellationToken = default (CancellationToken));

		/// <summary>
		/// Asynchronously mark the specified range of messages for deletion.
		/// </summary>
		/// <remarks>
		/// Messages marked for deletion are not actually deleted until the session
		/// is cleanly disconnected
		/// (see <see cref="IMailService.Disconnect(bool, CancellationToken)"/>).
		/// </remarks>
		/// <returns>An asynchronous task context.</returns>
		/// <param name="startIndex">The index of the first message to mark for deletion.</param>
		/// <param name="count">The number of messages to mark for deletion.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		Task DeleteMessagesAsync (int startIndex, int count, CancellationToken cancellationToken = default (CancellationToken));

		/// <summary>
		/// Mark all messages for deletion.
		/// </summary>
		/// <remarks>
		/// Messages marked for deletion are not actually deleted until the session
		/// is cleanly disconnected
		/// (see <see cref="IMailService.Disconnect(bool, CancellationToken)"/>).
		/// </remarks>
		/// <param name="cancellationToken">The cancellation token.</param>
		void DeleteAllMessages (CancellationToken cancellationToken = default (CancellationToken));

		/// <summary>
		/// Asynchronously mark all messages for deletion.
		/// </summary>
		/// <remarks>
		/// Messages marked for deletion are not actually deleted until the session
		/// is cleanly disconnected
		/// (see <see cref="IMailService.Disconnect(bool, CancellationToken)"/>).
		/// </remarks>
		/// <returns>An asynchronous task context.</returns>
		/// <param name="cancellationToken">The cancellation token.</param>
		Task DeleteAllMessagesAsync (CancellationToken cancellationToken = default (CancellationToken));

		/// <summary>
		/// Reset the state of all messages marked for deletion.
		/// </summary>
		/// <remarks>
		/// Messages marked for deletion are not actually deleted until the session
		/// is cleanly disconnected
		/// (see <see cref="IMailService.Disconnect(bool, CancellationToken)"/>).
		/// </remarks>
		/// <param name="cancellationToken">The cancellation token.</param>
		void Reset (CancellationToken cancellationToken = default (CancellationToken));

		/// <summary>
		/// Asynchronously reset the state of all messages marked for deletion.
		/// </summary>
		/// <remarks>
		/// Messages marked for deletion are not actually deleted until the session
		/// is cleanly disconnected
		/// (see <see cref="IMailService.Disconnect(bool, CancellationToken)"/>).
		/// </remarks>
		/// <returns>An asynchronous task context.</returns>
		/// <param name="cancellationToken">The cancellation token.</param>
		Task ResetAsync (CancellationToken cancellationToken = default (CancellationToken));
	}
}
