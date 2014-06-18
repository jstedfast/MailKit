//
// MailSpool.cs
//
// Author: Jeffrey Stedfast <jeff@xamarin.com>
//
// Copyright (c) 2014 Xamarin Inc. (www.xamarin.com)
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
using System.Collections;
using System.Threading.Tasks;
using System.Collections.Generic;

using MimeKit;

namespace MailKit {
	/// <summary>
	/// An abstract mail spool implementation.
	/// </summary>
	/// <remarks>
	/// An abstract mail spool implementation.
	/// </remarks>
	public abstract class MailSpool : MailService, IMailSpool
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
		public abstract bool SupportsUids {
			get;
		}

		/// <summary>
		/// Get the number of messages available in the message spool.
		/// </summary>
		/// <remarks>
		/// Gets the number of messages available in the message spool.
		/// </remarks>
		/// <returns>The number of available messages.</returns>
		/// <param name="cancellationToken">A cancellation token.</param>
		/// <exception cref="System.ObjectDisposedException">
		/// The <see cref="MailSpool"/> has been disposed.
		/// </exception>
		/// <exception cref="InvalidOperationException">
		/// The <see cref="MailSpool"/> is not connected.
		/// </exception>
		/// <exception cref="System.UnauthorizedAccessException">
		/// The <see cref="MailSpool"/> is not authenticated.
		/// </exception>
		/// <exception cref="System.OperationCanceledException">
		/// The operation was canceled via the cancellation token.
		/// </exception>
		/// <exception cref="System.IO.IOException">
		/// An I/O error occurred.
		/// </exception>
		/// <exception cref="CommandException">
		/// The command failed.
		/// </exception>
		/// <exception cref="ProtocolException">
		/// A protocol error occurred.
		/// </exception>
		public abstract int GetMessageCount (CancellationToken cancellationToken = default (CancellationToken));

		/// <summary>
		/// Asynchronously get the number of messages available in the message spool.
		/// </summary>
		/// <remarks>
		/// Asynchronously gets the number of messages available in the message spool.
		/// </remarks>
		/// <returns>The number of available messages.</returns>
		/// <param name="cancellationToken">A cancellation token.</param>
		/// <exception cref="System.ObjectDisposedException">
		/// The <see cref="MailSpool"/> has been disposed.
		/// </exception>
		/// <exception cref="InvalidOperationException">
		/// The <see cref="MailSpool"/> is not connected.
		/// </exception>
		/// <exception cref="System.UnauthorizedAccessException">
		/// The <see cref="MailSpool"/> is not authenticated.
		/// </exception>
		/// <exception cref="System.OperationCanceledException">
		/// The operation was canceled via the cancellation token.
		/// </exception>
		/// <exception cref="System.IO.IOException">
		/// An I/O error occurred.
		/// </exception>
		/// <exception cref="CommandException">
		/// The command failed.
		/// </exception>
		/// <exception cref="ProtocolException">
		/// A protocol error occurred.
		/// </exception>
		public virtual Task<int> GetMessageCountAsync (CancellationToken cancellationToken = default (CancellationToken))
		{
			return Task.Factory.StartNew (() => {
				return GetMessageCount (cancellationToken);
			}, cancellationToken, TaskCreationOptions.None, TaskScheduler.Default);
		}

		/// <summary>
		/// Get the UID of the message at the specified index.
		/// </summary>
		/// <remarks>
		/// Not all servers support UIDs, so you should first check
		/// the <see cref="SupportsUids"/> property.
		/// </remarks>
		/// <returns>The message UID.</returns>
		/// <param name="index">The message index.</param>
		/// <param name="cancellationToken">A cancellation token.</param>
		/// <exception cref="System.ArgumentOutOfRangeException">
		/// <paramref name="index"/> is not a valid message index.
		/// </exception>
		/// <exception cref="System.ObjectDisposedException">
		/// The <see cref="MailSpool"/> has been disposed.
		/// </exception>
		/// <exception cref="InvalidOperationException">
		/// The <see cref="MailSpool"/> is not connected.
		/// </exception>
		/// <exception cref="System.UnauthorizedAccessException">
		/// The <see cref="MailSpool"/> is not authenticated.
		/// </exception>
		/// <exception cref="System.NotSupportedException">
		/// The mail spool does not support UIDs.
		/// </exception>
		/// <exception cref="System.OperationCanceledException">
		/// The operation was canceled via the cancellation token.
		/// </exception>
		/// <exception cref="System.IO.IOException">
		/// An I/O error occurred.
		/// </exception>
		/// <exception cref="CommandException">
		/// The command failed.
		/// </exception>
		/// <exception cref="ProtocolException">
		/// A protocol error occurred.
		/// </exception>
		public abstract string GetMessageUid (int index, CancellationToken cancellationToken = default (CancellationToken));

		/// <summary>
		/// Asynchronously get the UID of the message at the specified index.
		/// </summary>
		/// <remarks>
		/// Not all servers support UIDs, so you should first check
		/// the <see cref="SupportsUids"/> property.
		/// </remarks>
		/// <returns>The message UID.</returns>
		/// <param name="index">The message index.</param>
		/// <param name="cancellationToken">A cancellation token.</param>
		/// <exception cref="System.ArgumentOutOfRangeException">
		/// <paramref name="index"/> is not a valid message index.
		/// </exception>
		/// <exception cref="System.ObjectDisposedException">
		/// The <see cref="MailSpool"/> has been disposed.
		/// </exception>
		/// <exception cref="InvalidOperationException">
		/// The <see cref="MailSpool"/> is not connected.
		/// </exception>
		/// <exception cref="System.UnauthorizedAccessException">
		/// The <see cref="MailSpool"/> is not authenticated.
		/// </exception>
		/// <exception cref="System.NotSupportedException">
		/// The mail spool does not support UIDs.
		/// </exception>
		/// <exception cref="System.OperationCanceledException">
		/// The operation was canceled via the cancellation token.
		/// </exception>
		/// <exception cref="System.IO.IOException">
		/// An I/O error occurred.
		/// </exception>
		/// <exception cref="CommandException">
		/// The command failed.
		/// </exception>
		/// <exception cref="ProtocolException">
		/// A protocol error occurred.
		/// </exception>
		public virtual Task<string> GetMessageUidAsync (int index, CancellationToken cancellationToken = default (CancellationToken))
		{
			return Task.Factory.StartNew (() => {
				return GetMessageUid (index, cancellationToken);
			}, cancellationToken, TaskCreationOptions.None, TaskScheduler.Default);
		}

		/// <summary>
		/// Get the full list of available message UIDs.
		/// </summary>
		/// <remarks>
		/// Not all servers support UIDs, so you should first check
		/// the <see cref="SupportsUids"/> property.
		/// </remarks>
		/// <returns>The message uids.</returns>
		/// <param name="cancellationToken">A cancellation token.</param>
		/// <exception cref="System.ObjectDisposedException">
		/// The <see cref="MailSpool"/> has been disposed.
		/// </exception>
		/// <exception cref="InvalidOperationException">
		/// The <see cref="MailSpool"/> is not connected.
		/// </exception>
		/// <exception cref="System.UnauthorizedAccessException">
		/// The <see cref="MailSpool"/> is not authenticated.
		/// </exception>
		/// <exception cref="System.NotSupportedException">
		/// The mail spool does not support UIDs.
		/// </exception>
		/// <exception cref="System.OperationCanceledException">
		/// The operation was canceled via the cancellation token.
		/// </exception>
		/// <exception cref="System.IO.IOException">
		/// An I/O error occurred.
		/// </exception>
		/// <exception cref="CommandException">
		/// The command failed.
		/// </exception>
		/// <exception cref="ProtocolException">
		/// A protocol error occurred.
		/// </exception>
		public abstract string[] GetMessageUids (CancellationToken cancellationToken = default (CancellationToken));

		/// <summary>
		/// Get the full list of available message UIDs.
		/// </summary>
		/// <remarks>
		/// Not all servers support UIDs, so you should first check
		/// the <see cref="SupportsUids"/> property.
		/// </remarks>
		/// <returns>The message uids.</returns>
		/// <param name="cancellationToken">A cancellation token.</param>
		/// <exception cref="System.ObjectDisposedException">
		/// The <see cref="MailSpool"/> has been disposed.
		/// </exception>
		/// <exception cref="InvalidOperationException">
		/// The <see cref="MailSpool"/> is not connected.
		/// </exception>
		/// <exception cref="System.UnauthorizedAccessException">
		/// The <see cref="MailSpool"/> is not authenticated.
		/// </exception>
		/// <exception cref="System.NotSupportedException">
		/// The mail spool does not support UIDs.
		/// </exception>
		/// <exception cref="System.OperationCanceledException">
		/// The operation was canceled via the cancellation token.
		/// </exception>
		/// <exception cref="System.IO.IOException">
		/// An I/O error occurred.
		/// </exception>
		/// <exception cref="CommandException">
		/// The command failed.
		/// </exception>
		/// <exception cref="ProtocolException">
		/// A protocol error occurred.
		/// </exception>
		public virtual Task<string[]> GetMessageUidsAsync (CancellationToken cancellationToken = default (CancellationToken))
		{
			return Task.Factory.StartNew (() => {
				return GetMessageUids (cancellationToken);
			}, cancellationToken, TaskCreationOptions.None, TaskScheduler.Default);
		}

		/// <summary>
		/// Get the size of the specified message, in bytes.
		/// </summary>
		/// <remarks>
		/// Gets the size of the specified message, in bytes.
		/// </remarks>
		/// <returns>The message size, in bytes.</returns>
		/// <param name="uid">The UID of the message.</param>
		/// <param name="cancellationToken">A cancellation token.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <paramref name="uid"/> is <c>null</c>.
		/// </exception>
		/// <exception cref="System.ArgumentException">
		/// <paramref name="uid"/> is not a valid message UID.
		/// </exception>
		/// <exception cref="System.ObjectDisposedException">
		/// The <see cref="MailSpool"/> has been disposed.
		/// </exception>
		/// <exception cref="System.InvalidOperationException">
		/// The <see cref="MailSpool"/> is not connected.
		/// </exception>
		/// <exception cref="System.UnauthorizedAccessException">
		/// The <see cref="MailSpool"/> is not authenticated.
		/// </exception>
		/// <exception cref="System.OperationCanceledException">
		/// The operation was canceled via the cancellation token.
		/// </exception>
		/// <exception cref="System.IO.IOException">
		/// An I/O error occurred.
		/// </exception>
		/// <exception cref="CommandException">
		/// The command failed.
		/// </exception>
		/// <exception cref="ProtocolException">
		/// A protocol error occurred.
		/// </exception>
		public abstract int GetMessageSize (string uid, CancellationToken cancellationToken = default (CancellationToken));

		/// <summary>
		/// Asynchronously get the size of the specified message, in bytes.
		/// </summary>
		/// <remarks>
		/// Asynchronously gets the size of the specified message, in bytes.
		/// </remarks>
		/// <returns>The message size, in bytes.</returns>
		/// <param name="uid">The UID of the message.</param>
		/// <param name="cancellationToken">A cancellation token.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <paramref name="uid"/> is <c>null</c>.
		/// </exception>
		/// <exception cref="System.ArgumentException">
		/// <paramref name="uid"/> is not a valid message UID.
		/// </exception>
		/// <exception cref="System.ObjectDisposedException">
		/// The <see cref="MailSpool"/> has been disposed.
		/// </exception>
		/// <exception cref="System.InvalidOperationException">
		/// The <see cref="MailSpool"/> is not connected.
		/// </exception>
		/// <exception cref="System.UnauthorizedAccessException">
		/// The <see cref="MailSpool"/> is not authenticated.
		/// </exception>
		/// <exception cref="System.OperationCanceledException">
		/// The operation was canceled via the cancellation token.
		/// </exception>
		/// <exception cref="System.IO.IOException">
		/// An I/O error occurred.
		/// </exception>
		/// <exception cref="CommandException">
		/// The command failed.
		/// </exception>
		/// <exception cref="ProtocolException">
		/// A protocol error occurred.
		/// </exception>
		public virtual Task<int> GetMessageSizeAsync (string uid, CancellationToken cancellationToken = default (CancellationToken))
		{
			return Task.Factory.StartNew (() => {
				return GetMessageSize (uid, cancellationToken);
			}, cancellationToken, TaskCreationOptions.None, TaskScheduler.Default);
		}

		/// <summary>
		/// Get the size of the specified message, in bytes.
		/// </summary>
		/// <remarks>
		/// Gets the size of the specified message, in bytes.
		/// </remarks>
		/// <returns>The message size, in bytes.</returns>
		/// <param name="index">The index of the message.</param>
		/// <param name="cancellationToken">A cancellation token.</param>
		/// <exception cref="System.ArgumentOutOfRangeException">
		/// <paramref name="index"/> is not a valid message index.
		/// </exception>
		/// <exception cref="System.ObjectDisposedException">
		/// The <see cref="MailSpool"/> has been disposed.
		/// </exception>
		/// <exception cref="System.InvalidOperationException">
		/// The <see cref="MailSpool"/> is not connected.
		/// </exception>
		/// <exception cref="System.UnauthorizedAccessException">
		/// The <see cref="MailSpool"/> is not authenticated.
		/// </exception>
		/// <exception cref="System.OperationCanceledException">
		/// The operation was canceled via the cancellation token.
		/// </exception>
		/// <exception cref="System.IO.IOException">
		/// An I/O error occurred.
		/// </exception>
		/// <exception cref="CommandException">
		/// The command failed.
		/// </exception>
		/// <exception cref="ProtocolException">
		/// A protocol error occurred.
		/// </exception>
		public abstract int GetMessageSize (int index, CancellationToken cancellationToken = default (CancellationToken));

		/// <summary>
		/// Asynchronously get the size of the specified message, in bytes.
		/// </summary>
		/// <remarks>
		/// Asynchronously gets the size of the specified message, in bytes.
		/// </remarks>
		/// <returns>The message size, in bytes.</returns>
		/// <param name="index">The index of the message.</param>
		/// <param name="cancellationToken">A cancellation token.</param>
		/// <exception cref="System.ArgumentOutOfRangeException">
		/// <paramref name="index"/> is not a valid message index.
		/// </exception>
		/// <exception cref="System.ObjectDisposedException">
		/// The <see cref="MailSpool"/> has been disposed.
		/// </exception>
		/// <exception cref="System.InvalidOperationException">
		/// The <see cref="MailSpool"/> is not connected.
		/// </exception>
		/// <exception cref="System.UnauthorizedAccessException">
		/// The <see cref="MailSpool"/> is not authenticated.
		/// </exception>
		/// <exception cref="System.OperationCanceledException">
		/// The operation was canceled via the cancellation token.
		/// </exception>
		/// <exception cref="System.IO.IOException">
		/// An I/O error occurred.
		/// </exception>
		/// <exception cref="CommandException">
		/// The command failed.
		/// </exception>
		/// <exception cref="ProtocolException">
		/// A protocol error occurred.
		/// </exception>
		public virtual Task<int> GetMessageSizeAsync (int index, CancellationToken cancellationToken = default (CancellationToken))
		{
			return Task.Factory.StartNew (() => {
				return GetMessageSize (index, cancellationToken);
			}, cancellationToken, TaskCreationOptions.None, TaskScheduler.Default);
		}

		/// <summary>
		/// Get the sizes for all available messages, in bytes.
		/// </summary>
		/// <remarks>
		/// Gets the sizes for all available messages, in bytes.
		/// </remarks>
		/// <returns>The message sizes, in bytes.</returns>
		/// <param name="cancellationToken">A cancellation token.</param>
		/// <exception cref="System.ObjectDisposedException">
		/// The <see cref="MailSpool"/> has been disposed.
		/// </exception>
		/// <exception cref="InvalidOperationException">
		/// The <see cref="MailSpool"/> is not connected.
		/// </exception>
		/// <exception cref="System.UnauthorizedAccessException">
		/// The <see cref="MailSpool"/> is not authenticated.
		/// </exception>
		/// <exception cref="System.OperationCanceledException">
		/// The operation was canceled via the cancellation token.
		/// </exception>
		/// <exception cref="System.IO.IOException">
		/// An I/O error occurred.
		/// </exception>
		/// <exception cref="CommandException">
		/// The command failed.
		/// </exception>
		/// <exception cref="ProtocolException">
		/// A protocol error occurred.
		/// </exception>
		public abstract int[] GetMessageSizes (CancellationToken cancellationToken = default (CancellationToken));

		/// <summary>
		/// Asynchronously get the sizes for all available messages, in bytes.
		/// </summary>
		/// <remarks>
		/// Asynchronously gets the sizes for all available messages, in bytes.
		/// </remarks>
		/// <returns>The message sizes, in bytes.</returns>
		/// <param name="cancellationToken">A cancellation token.</param>
		/// <exception cref="System.ObjectDisposedException">
		/// The <see cref="MailSpool"/> has been disposed.
		/// </exception>
		/// <exception cref="InvalidOperationException">
		/// The <see cref="MailSpool"/> is not connected.
		/// </exception>
		/// <exception cref="System.UnauthorizedAccessException">
		/// The <see cref="MailSpool"/> is not authenticated.
		/// </exception>
		/// <exception cref="System.OperationCanceledException">
		/// The operation was canceled via the cancellation token.
		/// </exception>
		/// <exception cref="System.IO.IOException">
		/// An I/O error occurred.
		/// </exception>
		/// <exception cref="CommandException">
		/// The command failed.
		/// </exception>
		/// <exception cref="ProtocolException">
		/// A protocol error occurred.
		/// </exception>
		public virtual Task<int[]> GetMessageSizesAsync (CancellationToken cancellationToken = default (CancellationToken))
		{
			return Task.Factory.StartNew (() => {
				return GetMessageSizes (cancellationToken);
			}, cancellationToken, TaskCreationOptions.None, TaskScheduler.Default);
		}

		/// <summary>
		/// Get the headers for the specified message.
		/// </summary>
		/// <remarks>
		/// Gets the headers for the specified message.
		/// </remarks>
		/// <returns>The message headers.</returns>
		/// <param name="uid">The UID of the message.</param>
		/// <param name="cancellationToken">A cancellation token.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <paramref name="uid"/> is <c>null</c>.
		/// </exception>
		/// <exception cref="System.ArgumentException">
		/// <paramref name="uid"/> is not a valid message UID.
		/// </exception>
		/// <exception cref="System.ObjectDisposedException">
		/// The <see cref="MailSpool"/> has been disposed.
		/// </exception>
		/// <exception cref="InvalidOperationException">
		/// The <see cref="MailSpool"/> is not connected.
		/// </exception>
		/// <exception cref="System.UnauthorizedAccessException">
		/// The <see cref="MailSpool"/> is not authenticated.
		/// </exception>
		/// <exception cref="System.NotSupportedException">
		/// The mail spool does not support UIDs.
		/// </exception>
		/// <exception cref="System.OperationCanceledException">
		/// The operation was canceled via the cancellation token.
		/// </exception>
		/// <exception cref="System.IO.IOException">
		/// An I/O error occurred.
		/// </exception>
		/// <exception cref="CommandException">
		/// The command failed.
		/// </exception>
		/// <exception cref="ProtocolException">
		/// A protocol error occurred.
		/// </exception>
		public abstract HeaderList GetMessageHeaders (string uid, CancellationToken cancellationToken = default (CancellationToken));

		/// <summary>
		/// Asynchronously get the headers for the specified message.
		/// </summary>
		/// <remarks>
		/// Asynchronously gets the headers for the specified message.
		/// </remarks>
		/// <returns>The message headers.</returns>
		/// <param name="uid">The UID of the message.</param>
		/// <param name="cancellationToken">A cancellation token.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <paramref name="uid"/> is <c>null</c>.
		/// </exception>
		/// <exception cref="System.ArgumentException">
		/// <paramref name="uid"/> is not a valid message UID.
		/// </exception>
		/// <exception cref="System.ObjectDisposedException">
		/// The <see cref="MailSpool"/> has been disposed.
		/// </exception>
		/// <exception cref="InvalidOperationException">
		/// The <see cref="MailSpool"/> is not connected.
		/// </exception>
		/// <exception cref="System.UnauthorizedAccessException">
		/// The <see cref="MailSpool"/> is not authenticated.
		/// </exception>
		/// <exception cref="System.NotSupportedException">
		/// The mail spool does not support UIDs.
		/// </exception>
		/// <exception cref="System.OperationCanceledException">
		/// The operation was canceled via the cancellation token.
		/// </exception>
		/// <exception cref="System.IO.IOException">
		/// An I/O error occurred.
		/// </exception>
		/// <exception cref="CommandException">
		/// The command failed.
		/// </exception>
		/// <exception cref="ProtocolException">
		/// A protocol error occurred.
		/// </exception>
		public virtual Task<HeaderList> GetMessageHeadersAsync (string uid, CancellationToken cancellationToken = default (CancellationToken))
		{
			return Task.Factory.StartNew (() => {
				return GetMessageHeaders (uid, cancellationToken);
			}, cancellationToken, TaskCreationOptions.None, TaskScheduler.Default);
		}

		/// <summary>
		/// Get the headers for the specified message.
		/// </summary>
		/// <remarks>
		/// Gets the headers for the specified message.
		/// </remarks>
		/// <returns>The message headers.</returns>
		/// <param name="index">The index of the message.</param>
		/// <param name="cancellationToken">A cancellation token.</param>
		/// <exception cref="System.ArgumentOutOfRangeException">
		/// <paramref name="index"/> is not a valid message index.
		/// </exception>
		/// <exception cref="System.ObjectDisposedException">
		/// The <see cref="MailSpool"/> has been disposed.
		/// </exception>
		/// <exception cref="InvalidOperationException">
		/// The <see cref="MailSpool"/> is not connected.
		/// </exception>
		/// <exception cref="System.UnauthorizedAccessException">
		/// The <see cref="MailSpool"/> is not authenticated.
		/// </exception>
		/// <exception cref="System.OperationCanceledException">
		/// The operation was canceled via the cancellation token.
		/// </exception>
		/// <exception cref="System.IO.IOException">
		/// An I/O error occurred.
		/// </exception>
		/// <exception cref="CommandException">
		/// The command failed.
		/// </exception>
		/// <exception cref="ProtocolException">
		/// A protocol error occurred.
		/// </exception>
		public abstract HeaderList GetMessageHeaders (int index, CancellationToken cancellationToken = default (CancellationToken));

		/// <summary>
		/// Asynchronously get the headers for the specified message.
		/// </summary>
		/// <remarks>
		/// Asynchronously gets the headers for the specified message.
		/// </remarks>
		/// <returns>The message headers.</returns>
		/// <param name="index">The index of the message.</param>
		/// <param name="cancellationToken">A cancellation token.</param>
		/// <exception cref="System.ArgumentOutOfRangeException">
		/// <paramref name="index"/> is not a valid message index.
		/// </exception>
		/// <exception cref="System.ObjectDisposedException">
		/// The <see cref="MailSpool"/> has been disposed.
		/// </exception>
		/// <exception cref="InvalidOperationException">
		/// The <see cref="MailSpool"/> is not connected.
		/// </exception>
		/// <exception cref="System.UnauthorizedAccessException">
		/// The <see cref="MailSpool"/> is not authenticated.
		/// </exception>
		/// <exception cref="System.OperationCanceledException">
		/// The operation was canceled via the cancellation token.
		/// </exception>
		/// <exception cref="System.IO.IOException">
		/// An I/O error occurred.
		/// </exception>
		/// <exception cref="CommandException">
		/// The command failed.
		/// </exception>
		/// <exception cref="ProtocolException">
		/// A protocol error occurred.
		/// </exception>
		public virtual Task<HeaderList> GetMessageHeadersAsync (int index, CancellationToken cancellationToken = default (CancellationToken))
		{
			return Task.Factory.StartNew (() => {
				return GetMessageHeaders (index, cancellationToken);
			}, cancellationToken, TaskCreationOptions.None, TaskScheduler.Default);
		}

		/// <summary>
		/// Get the message with the specified UID.
		/// </summary>
		/// <remarks>
		/// Gets the message with the specified UID.
		/// </remarks>
		/// <returns>The message.</returns>
		/// <param name="uid">The UID of the message.</param>
		/// <param name="cancellationToken">A cancellation token.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <paramref name="uid"/> is <c>null</c>.
		/// </exception>
		/// <exception cref="System.ArgumentException">
		/// <paramref name="uid"/> is not a valid message UID.
		/// </exception>
		/// <exception cref="System.ObjectDisposedException">
		/// The <see cref="MailSpool"/> has been disposed.
		/// </exception>
		/// <exception cref="InvalidOperationException">
		/// The <see cref="MailSpool"/> is not connected.
		/// </exception>
		/// <exception cref="System.UnauthorizedAccessException">
		/// The <see cref="MailSpool"/> is not authenticated.
		/// </exception>
		/// <exception cref="System.NotSupportedException">
		/// The mail spool does not support UIDs.
		/// </exception>
		/// <exception cref="System.OperationCanceledException">
		/// The operation was canceled via the cancellation token.
		/// </exception>
		/// <exception cref="System.IO.IOException">
		/// An I/O error occurred.
		/// </exception>
		/// <exception cref="CommandException">
		/// The command failed.
		/// </exception>
		/// <exception cref="ProtocolException">
		/// A protocol error occurred.
		/// </exception>
		public abstract MimeMessage GetMessage (string uid, CancellationToken cancellationToken = default (CancellationToken));

		/// <summary>
		/// Asynchronously get the message with the specified UID.
		/// </summary>
		/// <remarks>
		/// Asynchronously gets the message with the specified UID.
		/// </remarks>
		/// <returns>The message.</returns>
		/// <param name="uid">The UID of the message.</param>
		/// <param name="cancellationToken">A cancellation token.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <paramref name="uid"/> is <c>null</c>.
		/// </exception>
		/// <exception cref="System.ArgumentException">
		/// <paramref name="uid"/> is not a valid message UID.
		/// </exception>
		/// <exception cref="System.ObjectDisposedException">
		/// The <see cref="MailSpool"/> has been disposed.
		/// </exception>
		/// <exception cref="InvalidOperationException">
		/// The <see cref="MailSpool"/> is not connected.
		/// </exception>
		/// <exception cref="System.UnauthorizedAccessException">
		/// The <see cref="MailSpool"/> is not authenticated.
		/// </exception>
		/// <exception cref="System.NotSupportedException">
		/// The mail spool does not support UIDs.
		/// </exception>
		/// <exception cref="System.OperationCanceledException">
		/// The operation was canceled via the cancellation token.
		/// </exception>
		/// <exception cref="System.IO.IOException">
		/// An I/O error occurred.
		/// </exception>
		/// <exception cref="CommandException">
		/// The command failed.
		/// </exception>
		/// <exception cref="ProtocolException">
		/// A protocol error occurred.
		/// </exception>
		public virtual Task<MimeMessage> GetMessageAsync (string uid, CancellationToken cancellationToken = default (CancellationToken))
		{
			return Task.Factory.StartNew (() => {
				return GetMessage (uid, cancellationToken);
			}, cancellationToken, TaskCreationOptions.None, TaskScheduler.Default);
		}

		/// <summary>
		/// Get the message at the specified index.
		/// </summary>
		/// <remarks>
		/// Gets the message at the specified index.
		/// </remarks>
		/// <returns>The message.</returns>
		/// <param name="index">The index of the message.</param>
		/// <param name="cancellationToken">A cancellation token.</param>
		/// <exception cref="System.ArgumentOutOfRangeException">
		/// <paramref name="index"/> is not a valid message index.
		/// </exception>
		/// <exception cref="System.ObjectDisposedException">
		/// The <see cref="MailSpool"/> has been disposed.
		/// </exception>
		/// <exception cref="InvalidOperationException">
		/// The <see cref="MailSpool"/> is not connected.
		/// </exception>
		/// <exception cref="System.UnauthorizedAccessException">
		/// The <see cref="MailSpool"/> is not authenticated.
		/// </exception>
		/// <exception cref="System.OperationCanceledException">
		/// The operation was canceled via the cancellation token.
		/// </exception>
		/// <exception cref="System.IO.IOException">
		/// An I/O error occurred.
		/// </exception>
		/// <exception cref="CommandException">
		/// The command failed.
		/// </exception>
		/// <exception cref="ProtocolException">
		/// A protocol error occurred.
		/// </exception>
		public abstract MimeMessage GetMessage (int index, CancellationToken cancellationToken = default (CancellationToken));

		/// <summary>
		/// Asynchronously get the message at the specified index.
		/// </summary>
		/// <remarks>
		/// Asynchronously gets the message at the specified index.
		/// </remarks>
		/// <returns>The message.</returns>
		/// <param name="index">The index of the message.</param>
		/// <param name="cancellationToken">A cancellation token.</param>
		/// <exception cref="System.ArgumentOutOfRangeException">
		/// <paramref name="index"/> is not a valid message index.
		/// </exception>
		/// <exception cref="System.ObjectDisposedException">
		/// The <see cref="MailSpool"/> has been disposed.
		/// </exception>
		/// <exception cref="InvalidOperationException">
		/// The <see cref="MailSpool"/> is not connected.
		/// </exception>
		/// <exception cref="System.UnauthorizedAccessException">
		/// The <see cref="MailSpool"/> is not authenticated.
		/// </exception>
		/// <exception cref="System.OperationCanceledException">
		/// The operation was canceled via the cancellation token.
		/// </exception>
		/// <exception cref="System.IO.IOException">
		/// An I/O error occurred.
		/// </exception>
		/// <exception cref="CommandException">
		/// The command failed.
		/// </exception>
		/// <exception cref="ProtocolException">
		/// A protocol error occurred.
		/// </exception>
		public virtual Task<MimeMessage> GetMessageAsync (int index, CancellationToken cancellationToken = default (CancellationToken))
		{
			return Task.Factory.StartNew (() => {
				return GetMessage (index, cancellationToken);
			}, cancellationToken, TaskCreationOptions.None, TaskScheduler.Default);
		}

		/// <summary>
		/// Mark the specified message for deletion.
		/// </summary>
		/// <remarks>
		/// Messages marked for deletion are not actually deleted until the session
		/// is cleanly disconnected
		/// (see <see cref="IMailService.Disconnect(bool, CancellationToken)"/>).
		/// </remarks>
		/// <param name="uid">The UID of the message.</param>
		/// <param name="cancellationToken">A cancellation token.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <paramref name="uid"/> is <c>null</c>.
		/// </exception>
		/// <exception cref="System.ArgumentException">
		/// <paramref name="uid"/> is not a valid message UID.
		/// </exception>
		/// <exception cref="System.ObjectDisposedException">
		/// The <see cref="MailSpool"/> has been disposed.
		/// </exception>
		/// <exception cref="InvalidOperationException">
		/// The <see cref="MailSpool"/> is not connected.
		/// </exception>
		/// <exception cref="System.UnauthorizedAccessException">
		/// The <see cref="MailSpool"/> is not authenticated.
		/// </exception>
		/// <exception cref="System.NotSupportedException">
		/// The mail spool does not support UIDs.
		/// </exception>
		/// <exception cref="System.OperationCanceledException">
		/// The operation was canceled via the cancellation token.
		/// </exception>
		/// <exception cref="System.IO.IOException">
		/// An I/O error occurred.
		/// </exception>
		/// <exception cref="CommandException">
		/// The command failed.
		/// </exception>
		/// <exception cref="ProtocolException">
		/// A protocol error occurred.
		/// </exception>
		public abstract void DeleteMessage (string uid, CancellationToken cancellationToken = default (CancellationToken));

		/// <summary>
		/// Asynchronously mark the specified message for deletion.
		/// </summary>
		/// <remarks>
		/// Messages marked for deletion are not actually deleted until the session
		/// is cleanly disconnected
		/// (see <see cref="IMailService.Disconnect(bool, CancellationToken)"/>).
		/// </remarks>
		/// <param name="uid">The UID of the message.</param>
		/// <param name="cancellationToken">A cancellation token.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <paramref name="uid"/> is <c>null</c>.
		/// </exception>
		/// <exception cref="System.ArgumentException">
		/// <paramref name="uid"/> is not a valid message UID.
		/// </exception>
		/// <exception cref="System.ObjectDisposedException">
		/// The <see cref="MailSpool"/> has been disposed.
		/// </exception>
		/// <exception cref="InvalidOperationException">
		/// The <see cref="MailSpool"/> is not connected.
		/// </exception>
		/// <exception cref="System.UnauthorizedAccessException">
		/// The <see cref="MailSpool"/> is not authenticated.
		/// </exception>
		/// <exception cref="System.NotSupportedException">
		/// The mail spool does not support UIDs.
		/// </exception>
		/// <exception cref="System.OperationCanceledException">
		/// The operation was canceled via the cancellation token.
		/// </exception>
		/// <exception cref="System.IO.IOException">
		/// An I/O error occurred.
		/// </exception>
		/// <exception cref="CommandException">
		/// The command failed.
		/// </exception>
		/// <exception cref="ProtocolException">
		/// A protocol error occurred.
		/// </exception>
		public virtual Task DeleteMessageAsync (string uid, CancellationToken cancellationToken = default (CancellationToken))
		{
			return Task.Factory.StartNew (() => {
				DeleteMessage (uid, cancellationToken);
			}, cancellationToken, TaskCreationOptions.None, TaskScheduler.Default);
		}

		/// <summary>
		/// Mark the specified message for deletion.
		/// </summary>
		/// <remarks>
		/// Messages marked for deletion are not actually deleted until the session
		/// is cleanly disconnected
		/// (see <see cref="IMailService.Disconnect(bool, CancellationToken)"/>).
		/// </remarks>
		/// <param name="index">The index of the message.</param>
		/// <param name="cancellationToken">A cancellation token.</param>
		/// <exception cref="System.ArgumentOutOfRangeException">
		/// <paramref name="index"/> is not a valid message index.
		/// </exception>
		/// <exception cref="System.ObjectDisposedException">
		/// The <see cref="MailSpool"/> has been disposed.
		/// </exception>
		/// <exception cref="InvalidOperationException">
		/// The <see cref="MailSpool"/> is not connected.
		/// </exception>
		/// <exception cref="System.UnauthorizedAccessException">
		/// The <see cref="MailSpool"/> is not authenticated.
		/// </exception>
		/// <exception cref="System.OperationCanceledException">
		/// The operation was canceled via the cancellation token.
		/// </exception>
		/// <exception cref="System.IO.IOException">
		/// An I/O error occurred.
		/// </exception>
		/// <exception cref="CommandException">
		/// The command failed.
		/// </exception>
		/// <exception cref="ProtocolException">
		/// A protocol error occurred.
		/// </exception>
		public abstract void DeleteMessage (int index, CancellationToken cancellationToken = default (CancellationToken));

		/// <summary>
		/// Asynchronously mark the specified message for deletion.
		/// </summary>
		/// <remarks>
		/// Messages marked for deletion are not actually deleted until the session
		/// is cleanly disconnected
		/// (see <see cref="IMailService.Disconnect(bool, CancellationToken)"/>).
		/// </remarks>
		/// <param name="index">The index of the message.</param>
		/// <param name="cancellationToken">A cancellation token.</param>
		/// <exception cref="System.ArgumentOutOfRangeException">
		/// <paramref name="index"/> is not a valid message index.
		/// </exception>
		/// <exception cref="System.ObjectDisposedException">
		/// The <see cref="MailSpool"/> has been disposed.
		/// </exception>
		/// <exception cref="InvalidOperationException">
		/// The <see cref="MailSpool"/> is not connected.
		/// </exception>
		/// <exception cref="System.UnauthorizedAccessException">
		/// The <see cref="MailSpool"/> is not authenticated.
		/// </exception>
		/// <exception cref="System.OperationCanceledException">
		/// The operation was canceled via the cancellation token.
		/// </exception>
		/// <exception cref="System.IO.IOException">
		/// An I/O error occurred.
		/// </exception>
		/// <exception cref="CommandException">
		/// The command failed.
		/// </exception>
		/// <exception cref="ProtocolException">
		/// A protocol error occurred.
		/// </exception>
		public virtual Task DeleteMessageAsync (int index, CancellationToken cancellationToken = default (CancellationToken))
		{
			return Task.Factory.StartNew (() => {
				DeleteMessage (index, cancellationToken);
			}, cancellationToken, TaskCreationOptions.None, TaskScheduler.Default);
		}

		/// <summary>
		/// Reset the state of all messages marked for deletion.
		/// </summary>
		/// <remarks>
		/// Messages marked for deletion are not actually deleted until the session
		/// is cleanly disconnected
		/// (see <see cref="IMailService.Disconnect(bool, CancellationToken)"/>).
		/// </remarks>
		/// <param name="cancellationToken">A cancellation token.</param>
		/// <exception cref="System.ObjectDisposedException">
		/// The <see cref="MailSpool"/> has been disposed.
		/// </exception>
		/// <exception cref="InvalidOperationException">
		/// The <see cref="MailSpool"/> is not connected.
		/// </exception>
		/// <exception cref="System.UnauthorizedAccessException">
		/// The <see cref="MailSpool"/> is not authenticated.
		/// </exception>
		/// <exception cref="System.OperationCanceledException">
		/// The operation was canceled via the cancellation token.
		/// </exception>
		/// <exception cref="System.IO.IOException">
		/// An I/O error occurred.
		/// </exception>
		/// <exception cref="CommandException">
		/// The command failed.
		/// </exception>
		/// <exception cref="ProtocolException">
		/// A protocol error occurred.
		/// </exception>
		public abstract void Reset (CancellationToken cancellationToken = default (CancellationToken));

		/// <summary>
		/// Asynchronously reset the state of all messages marked for deletion.
		/// </summary>
		/// <remarks>
		/// Messages marked for deletion are not actually deleted until the session
		/// is cleanly disconnected
		/// (see <see cref="IMailService.Disconnect(bool, CancellationToken)"/>).
		/// </remarks>
		/// <param name="cancellationToken">A cancellation token.</param>
		/// <exception cref="System.ObjectDisposedException">
		/// The <see cref="MailSpool"/> has been disposed.
		/// </exception>
		/// <exception cref="InvalidOperationException">
		/// The <see cref="MailSpool"/> is not connected.
		/// </exception>
		/// <exception cref="System.UnauthorizedAccessException">
		/// The <see cref="MailSpool"/> is not authenticated.
		/// </exception>
		/// <exception cref="System.OperationCanceledException">
		/// The operation was canceled via the cancellation token.
		/// </exception>
		/// <exception cref="System.IO.IOException">
		/// An I/O error occurred.
		/// </exception>
		/// <exception cref="CommandException">
		/// The command failed.
		/// </exception>
		/// <exception cref="ProtocolException">
		/// A protocol error occurred.
		/// </exception>
		public Task ResetAsync (CancellationToken cancellationToken = default (CancellationToken))
		{
			return Task.Factory.StartNew (() => {
				Reset (cancellationToken);
			}, cancellationToken, TaskCreationOptions.None, TaskScheduler.Default);
		}

		/// <summary>
		/// Gets an enumerator for the messages in the folder.
		/// </summary>
		/// <remarks>
		/// Gets an enumerator for the messages in the folder.
		/// </remarks>
		/// <returns>The enumerator.</returns>
		/// <exception cref="System.ObjectDisposedException">
		/// The <see cref="MailSpool"/> has been disposed.
		/// </exception>
		/// <exception cref="InvalidOperationException">
		/// The <see cref="MailSpool"/> is not connected.
		/// </exception>
		/// <exception cref="System.UnauthorizedAccessException">
		/// The <see cref="MailSpool"/> is not authenticated.
		/// </exception>
		/// <exception cref="System.OperationCanceledException">
		/// The operation was canceled via the cancellation token.
		/// </exception>
		/// <exception cref="System.IO.IOException">
		/// An I/O error occurred.
		/// </exception>
		/// <exception cref="CommandException">
		/// A POP3 command failed.
		/// </exception>
		/// <exception cref="ProtocolException">
		/// A protocol error occurred.
		/// </exception>
		public abstract IEnumerator<MimeMessage> GetEnumerator ();

		/// <summary>
		/// Gets an enumerator for the messages in the folder.
		/// </summary>
		/// <remarks>
		/// Gets an enumerator for the messages in the folder.
		/// </remarks>
		/// <returns>The enumerator.</returns>
		/// <exception cref="System.ObjectDisposedException">
		/// The <see cref="MailSpool"/> has been disposed.
		/// </exception>
		/// <exception cref="InvalidOperationException">
		/// The <see cref="MailSpool"/> is not connected.
		/// </exception>
		/// <exception cref="System.UnauthorizedAccessException">
		/// The <see cref="MailSpool"/> is not authenticated.
		/// </exception>
		/// <exception cref="System.OperationCanceledException">
		/// The operation was canceled via the cancellation token.
		/// </exception>
		/// <exception cref="System.IO.IOException">
		/// An I/O error occurred.
		/// </exception>
		/// <exception cref="CommandException">
		/// A POP3 command failed.
		/// </exception>
		/// <exception cref="ProtocolException">
		/// A protocol error occurred.
		/// </exception>
		IEnumerator IEnumerable.GetEnumerator ()
		{
			return GetEnumerator ();
		}
	}
}
