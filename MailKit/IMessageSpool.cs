//
// IMessageSpool.cs
//
// Author: Jeffrey Stedfast <jeff@xamarin.com>
//
// Copyright (c) 2013 Jeffrey Stedfast
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
using System.Threading;
using System.Collections.Generic;

using MimeKit;

namespace MailKit {
	/// <summary>
	/// An interface for retreiving messages from a spool.
	/// </summary>
	/// <remarks>
	/// Implemented by <see cref="MailKit.Net.Pop3.Pop3Client"/>.
	/// </remarks>
	public interface IMessageSpool : IMessageService
	{
		/// <summary>
		/// Gets a value indicating whether this <see cref="MailKit.IMessageSpool"/> supports UIDs.
		/// </summary>
		/// <remarks>
		/// Not all servers support referencing messages by UID, so this property should
		/// be checked before using <see cref="GetMessageUid"/> and <see cref="GetMessageUids"/>.
		/// </remarks>
		/// <value><c>true</c> if supports uids; otherwise, <c>false</c>.</value>
		bool SupportsUids { get; }

		/// <summary>
		/// Gets the number of messages available in the message spool.
		/// </summary>
		/// <returns>The number of available messages.</returns>
		/// <param name="token">A cancellation token.</param>
		int Count (CancellationToken token);

		/// <summary>
		/// Gets the UID of the message at the specified index.
		/// </summary>
		/// <remarks>
		/// Not all servers support UIDs, so you should first check
		/// the <see cref="SupportsUids"/> property.
		/// </remarks>
		/// <returns>The message UID.</returns>
		/// <param name="index">The message index.</param>
		/// <param name="token">A cancellation token.</param>
		string GetMessageUid (int index, CancellationToken token);

		/// <summary>
		/// Gets the full list of available message UIDs.
		/// </summary>
		/// <remarks>
		/// Not all servers support UIDs, so you should first check
		/// the <see cref="SupportsUids"/> property.
		/// </remarks>
		/// <returns>The message UIDs.</returns>
		/// <param name="token">A cancellation token.</param>
		string[] GetMessageUids (CancellationToken token);

		/// <summary>
		/// Gets the size of the specified message, in bytes.
		/// </summary>
		/// <returns>The message size, in bytes.</returns>
		/// <param name="uid">The UID of the message.</param>
		/// <param name="token">A cancellation token.</param>
		int GetMessageSize (string uid, CancellationToken token);

		/// <summary>
		/// Gets the size of the specified message, in bytes.
		/// </summary>
		/// <returns>The message size, in bytes.</returns>
		/// <param name="index">The index of the message.</param>
		/// <param name="token">A cancellation token.</param>
		int GetMessageSize (int index, CancellationToken token);

		/// <summary>
		/// Gets the sizes for all available messages, in bytes.
		/// </summary>
		/// <returns>The message sizes, in bytes.</returns>
		/// <param name="token">A cancellation token.</param>
		int[] GetMessageSizes (CancellationToken token);

		/// <summary>
		/// Gets the headers for the specified message.
		/// </summary>
		/// <returns>The message headers.</returns>
		/// <param name="uid">The UID of the message.</param>
		/// <param name="token">A cancellation token.</param>
		HeaderList GetMessageHeaders (string uid, CancellationToken token);

		/// <summary>
		/// Gets the headers for the specified message.
		/// </summary>
		/// <returns>The message headers.</returns>
		/// <param name="index">The index of the message.</param>
		/// <param name="token">A cancellation token.</param>
		HeaderList GetMessageHeaders (int index, CancellationToken token);

		/// <summary>
		/// Gets the specified message.
		/// </summary>
		/// <returns>The message.</returns>
		/// <param name="uid">The UID of the message.</param>
		/// <param name="token">A cancellation token.</param>
		MimeMessage GetMessage (string uid, CancellationToken token);

		/// <summary>
		/// Gets the specified message.
		/// </summary>
		/// <returns>The message.</returns>
		/// <param name="index">The index of the message.</param>
		/// <param name="token">A cancellation token.</param>
		MimeMessage GetMessage (int index, CancellationToken token);

		/// <summary>
		/// Mark the specified message for deletion.
		/// </summary>
		/// <remarks>
		/// Messages marked for deletion are not actually deleted until the session
		/// is cleanly disconnected
		/// (see <see cref="IMessageService.Disconnect(bool, CancellationToken)"/>).
		/// </remarks>
		/// <param name="uid">The UID of the message.</param>
		/// <param name="token">A cancellation token.</param>
		void DeleteMessage (string uid, CancellationToken token);

		/// <summary>
		/// Mark the specified message for deletion.
		/// </summary>
		/// <remarks>
		/// Messages marked for deletion are not actually deleted until the session
		/// is cleanly disconnected
		/// (see <see cref="IMessageService.Disconnect(bool, CancellationToken)"/>).
		/// </remarks>
		/// <param name="index">The index of the message.</param>
		/// <param name="token">A cancellation token.</param>
		void DeleteMessage (int index, CancellationToken token);

		/// <summary>
		/// Reset the state of all messages marked for deletion.
		/// </summary>
		/// <remarks>
		/// Messages marked for deletion are not actually deleted until the session
		/// is cleanly disconnected
		/// (see <see cref="IMessageService.Disconnect(bool, CancellationToken)"/>).
		/// </remarks>
		/// <param name="token">A cancellation token.</param>
		void Reset (CancellationToken token);
	}
}
