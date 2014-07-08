//
// IMailSpool.cs
//
// Author: Jeffrey Stedfast <jeff@xamarin.com>
//
// Copyright (c) 2013-2014 Xamarin Inc. (www.xamarin.com)
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
		/// Get the number of messages available in the message spool.
		/// </summary>
		/// <remarks>
		/// Gets the number of messages available in the message spool.
		/// </remarks>
		/// <returns>The number of available messages.</returns>
		/// <param name="cancellationToken">The cancellation token.</param>
		int GetMessageCount (CancellationToken cancellationToken = default (CancellationToken));

		/// <summary>
		/// Asynchronously get the number of messages available in the message spool.
		/// </summary>
		/// <remarks>
		/// Asynchronously gets the number of messages available in the message spool.
		/// </remarks>
		/// <returns>The number of available messages.</returns>
		/// <param name="cancellationToken">The cancellation token.</param>
		Task<int> GetMessageCountAsync (CancellationToken cancellationToken = default (CancellationToken));

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
		/// <param name="uid">The UID of the message.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		int GetMessageSize (string uid, CancellationToken cancellationToken = default (CancellationToken));

		/// <summary>
		/// Asynchronously get the size of the specified message, in bytes.
		/// </summary>
		/// <remarks>
		/// Asynchronously gets the size of the specified message, in bytes.
		/// </remarks>
		/// <returns>The message size, in bytes.</returns>
		/// <param name="uid">The UID of the message.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		Task<int> GetMessageSizeAsync (string uid, CancellationToken cancellationToken = default (CancellationToken));

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
		/// <param name="uid">The UID of the message.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		HeaderList GetMessageHeaders (string uid, CancellationToken cancellationToken = default (CancellationToken));

		/// <summary>
		/// Asynchronously get the headers for the specified message.
		/// </summary>
		/// <remarks>
		/// Asynchronously gets the headers for the specified message.
		/// </remarks>
		/// <returns>The message headers.</returns>
		/// <param name="uid">The UID of the message.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		Task<HeaderList> GetMessageHeadersAsync (string uid, CancellationToken cancellationToken = default (CancellationToken));

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
		/// Get the message with the specified UID.
		/// </summary>
		/// <remarks>
		/// Gets the message with the specified UID.
		/// </remarks>
		/// <returns>The message.</returns>
		/// <param name="uid">The UID of the message.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		MimeMessage GetMessage (string uid, CancellationToken cancellationToken = default (CancellationToken));

		/// <summary>
		/// Asynchronously get the message with the specified UID.
		/// </summary>
		/// <remarks>
		/// Asynchronously gets the message with the specified UID.
		/// </remarks>
		/// <returns>The message.</returns>
		/// <param name="uid">The UID of the message.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		Task<MimeMessage> GetMessageAsync (string uid, CancellationToken cancellationToken = default (CancellationToken));

		/// <summary>
		/// Get the message at the specified index.
		/// </summary>
		/// <remarks>
		/// Get the message at the specified index.
		/// </remarks>
		/// <returns>The message.</returns>
		/// <param name="index">The index of the message.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		MimeMessage GetMessage (int index, CancellationToken cancellationToken = default (CancellationToken));

		/// <summary>
		/// Asynchronously get the message at the specified index.
		/// </summary>
		/// <remarks>
		/// Asynchronously get the message at the specified index.
		/// </remarks>
		/// <returns>The message.</returns>
		/// <param name="index">The index of the message.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		Task<MimeMessage> GetMessageAsync (int index, CancellationToken cancellationToken = default (CancellationToken));

		/// <summary>
		/// Get the messages with the specified UIDs.
		/// </summary>
		/// <remarks>
		/// Gets the messages with the specified UIDs.
		/// </remarks>
		/// <returns>The messages.</returns>
		/// <param name="uids">The UID of the messages.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		IList<MimeMessage> GetMessages (IList<string> uids, CancellationToken cancellationToken = default (CancellationToken));

		/// <summary>
		/// Asynchronously get the messages with the specified UIDs.
		/// </summary>
		/// <remarks>
		/// Asynchronously gets the messages with the specified UIDs.
		/// </remarks>
		/// <returns>The messages.</returns>
		/// <param name="uids">The UIDs of the messages.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		Task<IList<MimeMessage>> GetMessagesAsync (IList<string> uids, CancellationToken cancellationToken = default (CancellationToken));

		/// <summary>
		/// Get the messages at the specified indexes.
		/// </summary>
		/// <remarks>
		/// Get the messages at the specified indexes.
		/// </remarks>
		/// <returns>The messages.</returns>
		/// <param name="indexes">The indexes of the messages.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		IList<MimeMessage> GetMessages (IList<int> indexes, CancellationToken cancellationToken = default (CancellationToken));

		/// <summary>
		/// Asynchronously get the messages at the specified indexes.
		/// </summary>
		/// <remarks>
		/// Asynchronously get the messages at the specified indexes.
		/// </remarks>
		/// <returns>The messages.</returns>
		/// <param name="indexes">The indexes of the messages.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		Task<IList<MimeMessage>> GetMessagesAsync (IList<int> indexes, CancellationToken cancellationToken = default (CancellationToken));

		/// <summary>
		/// Mark the specified message for deletion.
		/// </summary>
		/// <remarks>
		/// Messages marked for deletion are not actually deleted until the session
		/// is cleanly disconnected
		/// (see <see cref="IMailService.Disconnect(bool, CancellationToken)"/>).
		/// </remarks>
		/// <param name="uid">The UID of the message.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		void DeleteMessage (string uid, CancellationToken cancellationToken = default (CancellationToken));

		/// <summary>
		/// Asynchronously mark the specified message for deletion.
		/// </summary>
		/// <remarks>
		/// Messages marked for deletion are not actually deleted until the session
		/// is cleanly disconnected
		/// (see <see cref="IMailService.Disconnect(bool, CancellationToken)"/>).
		/// </remarks>
		/// <returns>An asynchronous task context.</returns>
		/// <param name="uid">The UID of the message.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		Task DeleteMessageAsync (string uid, CancellationToken cancellationToken = default (CancellationToken));

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
		/// <param name="uids">The UIDs of the messages.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		void DeleteMessages (IList<string> uids, CancellationToken cancellationToken = default (CancellationToken));

		/// <summary>
		/// Asynchronously mark the specified messages for deletion.
		/// </summary>
		/// <remarks>
		/// Messages marked for deletion are not actually deleted until the session
		/// is cleanly disconnected
		/// (see <see cref="IMailService.Disconnect(bool, CancellationToken)"/>).
		/// </remarks>
		/// <returns>An asynchronous task context.</returns>
		/// <param name="uids">The UIDs of the messages.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		Task DeleteMessagesAsync (IList<string> uids, CancellationToken cancellationToken = default (CancellationToken));

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
