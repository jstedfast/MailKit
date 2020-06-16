//
// IImapClient.cs
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

using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace MailKit.Net.Imap {
	/// <summary>
	/// An interface for an IMAP client.
	/// </summary>
	/// <remarks>
	/// Implemented by <see cref="MailKit.Net.Imap.ImapClient"/>.
	/// </remarks>
	public interface IImapClient : IMailStore
	{
		/// <summary>
		/// Get the capabilities supported by the IMAP server.
		/// </summary>
		/// <remarks>
		/// The capabilities will not be known until a successful connection has been made via one of
		/// the <a href="Overload_MailKit_Net_Imap_ImapClient_Connect.htm">Connect</a> methods and may
		/// change as a side-effect of calling one of the
		/// <a href="Overload_MailKit_Net_Imap_ImapClient_Authenticate.htm">Authenticate</a>
		/// methods.
		/// </remarks>
		/// <example>
		/// <code language="c#" source="Examples\ImapExamples.cs" region="Capabilities"/>
		/// </example>
		/// <value>The capabilities.</value>
		/// <exception cref="System.ArgumentException">
		/// Capabilities cannot be enabled, they may only be disabled.
		/// </exception>
		ImapCapabilities Capabilities { get; set; }

		/// <summary>
		/// Gets the maximum size of a message that can be appended to a folder.
		/// </summary>
		/// <remarks>
		/// <para>Gets the maximum size of a message, in bytes, that can be appended to a folder.</para>
		/// <note type="note">If the value is not set, then the limit is unspecified.</note>
		/// </remarks>
		/// <value>The append limit.</value>
		uint? AppendLimit { get; }

		/// <summary>
		/// Gets the internationalization level supported by the IMAP server.
		/// </summary>
		/// <remarks>
		/// <para>Gets the internationalization level supported by the IMAP server.</para>
		/// <para>For more information, see
		/// <a href="https://tools.ietf.org/html/rfc5255#section-4">section 4 of rfc5255</a>.</para>
		/// </remarks>
		/// <value>The internationalization level.</value>
		int InternationalizationLevel { get; }

		/// <summary>
		/// Get the access rights supported by the IMAP server.
		/// </summary>
		/// <remarks>
		/// These rights are additional rights supported by the IMAP server beyond the standard rights
		/// defined in <a href="https://tools.ietf.org/html/rfc4314#section-2.1">section 2.1 of rfc4314</a>
		/// and will not be populated until the client is successfully connected.
		/// </remarks>
		/// <example>
		/// <code language="c#" source="Examples\ImapExamples.cs" region="Capabilities"/>
		/// </example>
		/// <value>The rights.</value>
		AccessRights Rights { get; }

		/// <summary>
		/// Get whether or not the client is currently in the IDLE state.
		/// </summary>
		/// <remarks>
		/// Gets whether or not the client is currently in the IDLE state.
		/// </remarks>
		/// <value><c>true</c> if an IDLE command is active; otherwise, <c>false</c>.</value>
		bool IsIdle { get; }

		/// <summary>
		/// Enable compression over the IMAP connection.
		/// </summary>
		/// <remarks>
		/// <para>Enables compression over the IMAP connection.</para>
		/// <para>If the IMAP server supports the <see cref="ImapCapabilities.Compress"/> extension,
		/// it is possible at any point after connecting to enable compression to reduce network
		/// bandwidth usage. Ideally, this method should be called before authenticating.</para>
		/// </remarks>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.ObjectDisposedException">
		/// The <see cref="ImapClient"/> has been disposed.
		/// </exception>
		/// <exception cref="ServiceNotConnectedException">
		/// The <see cref="ImapClient"/> is not connected.
		/// </exception>
		/// <exception cref="System.InvalidOperationException">
		/// Compression must be enabled before a folder has been selected.
		/// </exception>
		/// <exception cref="System.NotSupportedException">
		/// The IMAP server does not support the <see cref="ImapCapabilities.Compress"/> extension.
		/// </exception>
		/// <exception cref="System.OperationCanceledException">
		/// The operation was canceled via the cancellation token.
		/// </exception>
		/// <exception cref="System.IO.IOException">
		/// An I/O error occurred.
		/// </exception>
		/// <exception cref="ImapCommandException">
		/// The server replied to the COMPRESS command with a NO or BAD response.
		/// </exception>
		/// <exception cref="ImapProtocolException">
		/// An IMAP protocol error occurred.
		/// </exception>
		void Compress (CancellationToken cancellationToken = default (CancellationToken));

		/// <summary>
		/// Asynchronously enable compression over the IMAP connection.
		/// </summary>
		/// <remarks>
		/// <para>Asynchronously enables compression over the IMAP connection.</para>
		/// <para>If the IMAP server supports the <see cref="ImapCapabilities.Compress"/> extension,
		/// it is possible at any point after connecting to enable compression to reduce network
		/// bandwidth usage. Ideally, this method should be called before authenticating.</para>
		/// </remarks>
		/// <returns>An asynchronous task context.</returns>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.ObjectDisposedException">
		/// The <see cref="ImapClient"/> has been disposed.
		/// </exception>
		/// <exception cref="ServiceNotConnectedException">
		/// The <see cref="ImapClient"/> is not connected.
		/// </exception>
		/// <exception cref="System.InvalidOperationException">
		/// Compression must be enabled before a folder has been selected.
		/// </exception>
		/// <exception cref="System.NotSupportedException">
		/// The IMAP server does not support the COMPRESS extension.
		/// </exception>
		/// <exception cref="System.OperationCanceledException">
		/// The operation was canceled via the cancellation token.
		/// </exception>
		/// <exception cref="System.IO.IOException">
		/// An I/O error occurred.
		/// </exception>
		/// <exception cref="ImapCommandException">
		/// The server replied to the COMPRESS command with a NO or BAD response.
		/// </exception>
		/// <exception cref="ImapProtocolException">
		/// An IMAP protocol error occurred.
		/// </exception>
		Task CompressAsync (CancellationToken cancellationToken = default (CancellationToken));

		/// <summary>
		/// Enable the UTF8=ACCEPT extension.
		/// </summary>
		/// <remarks>
		/// Enables the <a href="https://tools.ietf.org/html/rfc6855">UTF8=ACCEPT</a> extension.
		/// </remarks>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.ObjectDisposedException">
		/// The <see cref="ImapClient"/> has been disposed.
		/// </exception>
		/// <exception cref="ServiceNotConnectedException">
		/// The <see cref="ImapClient"/> is not connected.
		/// </exception>
		/// <exception cref="ServiceNotAuthenticatedException">
		/// The <see cref="ImapClient"/> is not authenticated.
		/// </exception>
		/// <exception cref="System.InvalidOperationException">
		/// UTF8=ACCEPT needs to be enabled before selecting a folder.
		/// </exception>
		/// <exception cref="System.NotSupportedException">
		/// The IMAP server does not support the UTF8=ACCEPT extension.
		/// </exception>
		/// <exception cref="System.OperationCanceledException">
		/// The operation was canceled via the cancellation token.
		/// </exception>
		/// <exception cref="System.IO.IOException">
		/// An I/O error occurred.
		/// </exception>
		/// <exception cref="ImapCommandException">
		/// The server replied to the ENABLE command with a NO or BAD response.
		/// </exception>
		/// <exception cref="ImapProtocolException">
		/// An IMAP protocol error occurred.
		/// </exception>
		void EnableUTF8 (CancellationToken cancellationToken = default (CancellationToken));

		/// <summary>
		/// Asynchronously enable the UTF8=ACCEPT extension.
		/// </summary>
		/// <remarks>
		/// Enables the <a href="https://tools.ietf.org/html/rfc6855">UTF8=ACCEPT</a> extension.
		/// </remarks>
		/// <returns>An asynchronous task context.</returns>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.ObjectDisposedException">
		/// The <see cref="ImapClient"/> has been disposed.
		/// </exception>
		/// <exception cref="ServiceNotConnectedException">
		/// The <see cref="ImapClient"/> is not connected.
		/// </exception>
		/// <exception cref="ServiceNotAuthenticatedException">
		/// The <see cref="ImapClient"/> is not authenticated.
		/// </exception>
		/// <exception cref="System.InvalidOperationException">
		/// UTF8=ACCEPT needs to be enabled before selecting a folder.
		/// </exception>
		/// <exception cref="System.NotSupportedException">
		/// The IMAP server does not support the UTF8=ACCEPT extension.
		/// </exception>
		/// <exception cref="System.OperationCanceledException">
		/// The operation was canceled via the cancellation token.
		/// </exception>
		/// <exception cref="System.IO.IOException">
		/// An I/O error occurred.
		/// </exception>
		/// <exception cref="ImapCommandException">
		/// The server replied to the ENABLE command with a NO or BAD response.
		/// </exception>
		/// <exception cref="ImapProtocolException">
		/// An IMAP protocol error occurred.
		/// </exception>
		Task EnableUTF8Async (CancellationToken cancellationToken = default (CancellationToken));

		/// <summary>
		/// Identify the client implementation to the server and obtain the server implementation details.
		/// </summary>
		/// <remarks>
		/// <para>Passes along the client implementation details to the server while also obtaining implementation
		/// details from the server.</para>
		/// <para>If the <paramref name="clientImplementation"/> is <c>null</c> or no properties have been set, no
		/// identifying information will be sent to the server.</para>
		/// <note type="security">
		/// <para>Security Implications</para>
		/// <para>This command has the danger of violating the privacy of users if misused. Clients should
		/// notify users that they send the ID command.</para>
		/// <para>It is highly desirable that implementations provide a method of disabling ID support, perhaps by
		/// not calling this method at all, or by passing <c>null</c> as the <paramref name="clientImplementation"/>
		/// argument.</para>
		/// <para>Implementors must exercise extreme care in adding properties to the <paramref name="clientImplementation"/>.
		/// Some properties, such as a processor ID number, Ethernet address, or other unique (or mostly unique) identifier
		/// would allow tracking of users in ways that violate user privacy expectations and may also make it easier for
		/// attackers to exploit security holes in the client.</para>
		/// </note>
		/// </remarks>
		/// <example>
		/// <code language="c#" source="Examples\ImapExamples.cs" region="Capabilities"/>
		/// </example>
		/// <returns>The implementation details of the server if available; otherwise, <c>null</c>.</returns>
		/// <param name="clientImplementation">The client implementation.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.ObjectDisposedException">
		/// The <see cref="ImapClient"/> has been disposed.
		/// </exception>
		/// <exception cref="ServiceNotConnectedException">
		/// The <see cref="ImapClient"/> is not connected.
		/// </exception>
		/// <exception cref="System.NotSupportedException">
		/// The IMAP server does not support the ID extension.
		/// </exception>
		/// <exception cref="System.OperationCanceledException">
		/// The operation was canceled via the cancellation token.
		/// </exception>
		/// <exception cref="System.IO.IOException">
		/// An I/O error occurred.
		/// </exception>
		/// <exception cref="ImapCommandException">
		/// The server replied to the ID command with a NO or BAD response.
		/// </exception>
		/// <exception cref="ImapProtocolException">
		/// An IMAP protocol error occurred.
		/// </exception>
		ImapImplementation Identify (ImapImplementation clientImplementation, CancellationToken cancellationToken = default (CancellationToken));

		/// <summary>
		/// Asynchronously identify the client implementation to the server and obtain the server implementation details.
		/// </summary>
		/// <remarks>
		/// <para>Passes along the client implementation details to the server while also obtaining implementation
		/// details from the server.</para>
		/// <para>If the <paramref name="clientImplementation"/> is <c>null</c> or no properties have been set, no
		/// identifying information will be sent to the server.</para>
		/// <note type="security">
		/// <para>Security Implications</para>
		/// <para>This command has the danger of violating the privacy of users if misused. Clients should
		/// notify users that they send the ID command.</para>
		/// <para>It is highly desirable that implementations provide a method of disabling ID support, perhaps by
		/// not calling this method at all, or by passing <c>null</c> as the <paramref name="clientImplementation"/>
		/// argument.</para>
		/// <para>Implementors must exercise extreme care in adding properties to the <paramref name="clientImplementation"/>.
		/// Some properties, such as a processor ID number, Ethernet address, or other unique (or mostly unique) identifier
		/// would allow tracking of users in ways that violate user privacy expectations and may also make it easier for
		/// attackers to exploit security holes in the client.</para>
		/// </note>
		/// </remarks>
		/// <returns>The implementation details of the server if available; otherwise, <c>null</c>.</returns>
		/// <param name="clientImplementation">The client implementation.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.ObjectDisposedException">
		/// The <see cref="ImapClient"/> has been disposed.
		/// </exception>
		/// <exception cref="ServiceNotConnectedException">
		/// The <see cref="ImapClient"/> is not connected.
		/// </exception>
		/// <exception cref="System.NotSupportedException">
		/// The IMAP server does not support the ID extension.
		/// </exception>
		/// <exception cref="System.OperationCanceledException">
		/// The operation was canceled via the cancellation token.
		/// </exception>
		/// <exception cref="System.IO.IOException">
		/// An I/O error occurred.
		/// </exception>
		/// <exception cref="ImapCommandException">
		/// The server replied to the ID command with a NO or BAD response.
		/// </exception>
		/// <exception cref="ImapProtocolException">
		/// An IMAP protocol error occurred.
		/// </exception>
		Task<ImapImplementation> IdentifyAsync (ImapImplementation clientImplementation, CancellationToken cancellationToken = default (CancellationToken));

		/// <summary>
		/// Toggle the <see cref="ImapClient"/> into the IDLE state.
		/// </summary>
		/// <remarks>
		/// <para>When a client enters the IDLE state, the IMAP server will send
		/// events to the client as they occur on the selected folder. These events
		/// may include notifications of new messages arriving, expunge notifications,
		/// flag changes, etc.</para>
		/// <para>Due to the nature of the IDLE command, a folder must be selected
		/// before a client can enter into the IDLE state. This can be done by
		/// opening a folder using
		/// <see cref="MailKit.MailFolder.Open(FolderAccess,System.Threading.CancellationToken)"/>
		/// or any of the other variants.</para>
		/// <para>While the IDLE command is running, no other commands may be issued until the
		/// <paramref name="doneToken"/> is cancelled.</para>
		/// <note type="note">It is especially important to cancel the <paramref name="doneToken"/>
		/// before cancelling the <paramref name="cancellationToken"/> when using SSL or TLS due to
		/// the fact that <see cref="System.Net.Security.SslStream"/> cannot be polled.</note>
		/// </remarks>
		/// <example>
		/// <code language="c#" source="Examples\ImapIdleExample.cs"/>
		/// </example>
		/// <param name="doneToken">The cancellation token used to return to the non-idle state.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.ArgumentException">
		/// <paramref name="doneToken"/> must be cancellable (i.e. <see cref="System.Threading.CancellationToken.None"/> cannot be used).
		/// </exception>
		/// <exception cref="System.ObjectDisposedException">
		/// The <see cref="ImapClient"/> has been disposed.
		/// </exception>
		/// <exception cref="ServiceNotConnectedException">
		/// The <see cref="ImapClient"/> is not connected.
		/// </exception>
		/// <exception cref="ServiceNotAuthenticatedException">
		/// The <see cref="ImapClient"/> is not authenticated.
		/// </exception>
		/// <exception cref="System.InvalidOperationException">
		/// A <see cref="ImapFolder"/> has not been opened.
		/// </exception>
		/// <exception cref="System.NotSupportedException">
		/// The IMAP server does not support the IDLE extension.
		/// </exception>
		/// <exception cref="System.OperationCanceledException">
		/// The operation was canceled via the cancellation token.
		/// </exception>
		/// <exception cref="System.IO.IOException">
		/// An I/O error occurred.
		/// </exception>
		/// <exception cref="ImapCommandException">
		/// The server replied to the IDLE command with a NO or BAD response.
		/// </exception>
		/// <exception cref="ImapProtocolException">
		/// The server responded with an unexpected token.
		/// </exception>
		void Idle (CancellationToken doneToken, CancellationToken cancellationToken = default (CancellationToken));

		/// <summary>
		/// Asynchronously toggle the <see cref="ImapClient"/> into the IDLE state.
		/// </summary>
		/// <remarks>
		/// <para>When a client enters the IDLE state, the IMAP server will send
		/// events to the client as they occur on the selected folder. These events
		/// may include notifications of new messages arriving, expunge notifications,
		/// flag changes, etc.</para>
		/// <para>Due to the nature of the IDLE command, a folder must be selected
		/// before a client can enter into the IDLE state. This can be done by
		/// opening a folder using
		/// <see cref="MailKit.MailFolder.Open(FolderAccess,System.Threading.CancellationToken)"/>
		/// or any of the other variants.</para>
		/// <para>While the IDLE command is running, no other commands may be issued until the
		/// <paramref name="doneToken"/> is cancelled.</para>
		/// <note type="note">It is especially important to cancel the <paramref name="doneToken"/>
		/// before cancelling the <paramref name="cancellationToken"/> when using SSL or TLS due to
		/// the fact that <see cref="System.Net.Security.SslStream"/> cannot be polled.</note>
		/// </remarks>
		/// <returns>An asynchronous task context.</returns>
		/// <param name="doneToken">The cancellation token used to return to the non-idle state.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.ArgumentException">
		/// <paramref name="doneToken"/> must be cancellable (i.e. <see cref="System.Threading.CancellationToken.None"/> cannot be used).
		/// </exception>
		/// <exception cref="System.ObjectDisposedException">
		/// The <see cref="ImapClient"/> has been disposed.
		/// </exception>
		/// <exception cref="ServiceNotConnectedException">
		/// The <see cref="ImapClient"/> is not connected.
		/// </exception>
		/// <exception cref="ServiceNotAuthenticatedException">
		/// The <see cref="ImapClient"/> is not authenticated.
		/// </exception>
		/// <exception cref="System.InvalidOperationException">
		/// A <see cref="ImapFolder"/> has not been opened.
		/// </exception>
		/// <exception cref="System.NotSupportedException">
		/// The IMAP server does not support the IDLE extension.
		/// </exception>
		/// <exception cref="System.OperationCanceledException">
		/// The operation was canceled via the cancellation token.
		/// </exception>
		/// <exception cref="System.IO.IOException">
		/// An I/O error occurred.
		/// </exception>
		/// <exception cref="ImapCommandException">
		/// The server replied to the IDLE command with a NO or BAD response.
		/// </exception>
		/// <exception cref="ImapProtocolException">
		/// The server responded with an unexpected token.
		/// </exception>
		Task IdleAsync (CancellationToken doneToken, CancellationToken cancellationToken = default (CancellationToken));

		/// <summary>
		/// Request the specified notification events from the IMAP server.
		/// </summary>
		/// <remarks>
		/// <para>The <a href="https://tools.ietf.org/html/rfc5465">NOTIFY</a> command is used to expand
		/// which notifications the client wishes to be notified about, including status notifications
		/// about folders other than the currently selected folder. It can also be used to automatically
		/// FETCH information about new messages that have arrived in the currently selected folder.</para>
		/// <para>This, combined with <see cref="Idle(CancellationToken, CancellationToken)"/>,
		/// can be used to get instant notifications for changes to any of the specified folders.</para>
		/// </remarks>
		/// <param name="status"><c>true</c> if the server should immediately notify the client of the
		/// selected folder's status; otherwise, <c>false</c>.</param>
		/// <param name="eventGroups">The specific event groups that the client would like to receive notifications for.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <paramref name="eventGroups"/> is <c>null</c>.
		/// </exception>
		/// <exception cref="System.ArgumentException">
		/// <paramref name="eventGroups"/> is empty.
		/// </exception>
		/// <exception cref="System.ObjectDisposedException">
		/// The <see cref="ImapClient"/> has been disposed.
		/// </exception>
		/// <exception cref="ServiceNotConnectedException">
		/// The <see cref="ImapClient"/> is not connected.
		/// </exception>
		/// <exception cref="ServiceNotAuthenticatedException">
		/// The <see cref="ImapClient"/> is not authenticated.
		/// </exception>
		/// <exception cref="System.InvalidOperationException">
		/// One or more <see cref="ImapEventGroup"/> is invalid.
		/// </exception>
		/// <exception cref="System.NotSupportedException">
		/// The IMAP server does not support the NOTIFY extension.
		/// </exception>
		/// <exception cref="System.OperationCanceledException">
		/// The operation was canceled via the cancellation token.
		/// </exception>
		/// <exception cref="System.IO.IOException">
		/// An I/O error occurred.
		/// </exception>
		/// <exception cref="ImapCommandException">
		/// The server replied to the NOTIFY command with a NO or BAD response.
		/// </exception>
		/// <exception cref="ImapProtocolException">
		/// The server responded with an unexpected token.
		/// </exception>
		void Notify (bool status, IList<ImapEventGroup> eventGroups, CancellationToken cancellationToken = default (CancellationToken));

		/// <summary>
		/// Asynchronously request the specified notification events from the IMAP server.
		/// </summary>
		/// <remarks>
		/// <para>The <a href="https://tools.ietf.org/html/rfc5465">NOTIFY</a> command is used to expand
		/// which notifications the client wishes to be notified about, including status notifications
		/// about folders other than the currently selected folder. It can also be used to automatically
		/// FETCH information about new messages that have arrived in the currently selected folder.</para>
		/// <para>This, combined with <see cref="IdleAsync(CancellationToken, CancellationToken)"/>,
		/// can be used to get instant notifications for changes to any of the specified folders.</para>
		/// </remarks>
		/// <returns>An asynchronous task context.</returns>
		/// <param name="status"><c>true</c> if the server should immediately notify the client of the
		/// selected folder's status; otherwise, <c>false</c>.</param>
		/// <param name="eventGroups">The specific event groups that the client would like to receive notifications for.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <paramref name="eventGroups"/> is <c>null</c>.
		/// </exception>
		/// <exception cref="System.ArgumentException">
		/// <paramref name="eventGroups"/> is empty.
		/// </exception>
		/// <exception cref="System.ObjectDisposedException">
		/// The <see cref="ImapClient"/> has been disposed.
		/// </exception>
		/// <exception cref="ServiceNotConnectedException">
		/// The <see cref="ImapClient"/> is not connected.
		/// </exception>
		/// <exception cref="ServiceNotAuthenticatedException">
		/// The <see cref="ImapClient"/> is not authenticated.
		/// </exception>
		/// <exception cref="System.InvalidOperationException">
		/// One or more <see cref="ImapEventGroup"/> is invalid.
		/// </exception>
		/// <exception cref="System.NotSupportedException">
		/// The IMAP server does not support the NOTIFY extension.
		/// </exception>
		/// <exception cref="System.OperationCanceledException">
		/// The operation was canceled via the cancellation token.
		/// </exception>
		/// <exception cref="System.IO.IOException">
		/// An I/O error occurred.
		/// </exception>
		/// <exception cref="ImapCommandException">
		/// The server replied to the NOTIFY command with a NO or BAD response.
		/// </exception>
		/// <exception cref="ImapProtocolException">
		/// The server responded with an unexpected token.
		/// </exception>
		Task NotifyAsync (bool status, IList<ImapEventGroup> eventGroups, CancellationToken cancellationToken = default (CancellationToken));

		/// <summary>
		/// Disable any previously requested notification events from the IMAP server.
		/// </summary>
		/// <remarks>
		/// Disables any notification events requested in a prior call to 
		/// <see cref="Notify(bool, IList{ImapEventGroup}, CancellationToken)"/>.
		/// request.
		/// </remarks>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.ObjectDisposedException">
		/// The <see cref="ImapClient"/> has been disposed.
		/// </exception>
		/// <exception cref="ServiceNotConnectedException">
		/// The <see cref="ImapClient"/> is not connected.
		/// </exception>
		/// <exception cref="ServiceNotAuthenticatedException">
		/// The <see cref="ImapClient"/> is not authenticated.
		/// </exception>
		/// <exception cref="System.NotSupportedException">
		/// The IMAP server does not support the NOTIFY extension.
		/// </exception>
		/// <exception cref="System.OperationCanceledException">
		/// The operation was canceled via the cancellation token.
		/// </exception>
		/// <exception cref="System.IO.IOException">
		/// An I/O error occurred.
		/// </exception>
		/// <exception cref="ImapCommandException">
		/// The server replied to the NOTIFY command with a NO or BAD response.
		/// </exception>
		/// <exception cref="ImapProtocolException">
		/// The server responded with an unexpected token.
		/// </exception>
		void DisableNotify (CancellationToken cancellationToken = default (CancellationToken));

		/// <summary>
		/// Asynchronously disable any previously requested notification events from the IMAP server.
		/// </summary>
		/// <remarks>
		/// Disables any notification events requested in a prior call to 
		/// <see cref="NotifyAsync(bool, IList{ImapEventGroup}, CancellationToken)"/>.
		/// request.
		/// </remarks>
		/// <returns>An asynchronous task context.</returns>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.ObjectDisposedException">
		/// The <see cref="ImapClient"/> has been disposed.
		/// </exception>
		/// <exception cref="ServiceNotConnectedException">
		/// The <see cref="ImapClient"/> is not connected.
		/// </exception>
		/// <exception cref="ServiceNotAuthenticatedException">
		/// The <see cref="ImapClient"/> is not authenticated.
		/// </exception>
		/// <exception cref="System.NotSupportedException">
		/// The IMAP server does not support the NOTIFY extension.
		/// </exception>
		/// <exception cref="System.OperationCanceledException">
		/// The operation was canceled via the cancellation token.
		/// </exception>
		/// <exception cref="System.IO.IOException">
		/// An I/O error occurred.
		/// </exception>
		/// <exception cref="ImapCommandException">
		/// The server replied to the NOTIFY command with a NO or BAD response.
		/// </exception>
		/// <exception cref="ImapProtocolException">
		/// The server responded with an unexpected token.
		/// </exception>
		Task DisableNotifyAsync (CancellationToken cancellationToken = default (CancellationToken));
	}
}
