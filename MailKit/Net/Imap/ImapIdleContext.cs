//
// ImapIdleContext.cs
//
// Author: Jeffrey Stedfast <jestedfa@microsoft.com>
//
// Copyright (c) 2013-2024 .NET Foundation and Contributors
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
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace MailKit.Net.Imap {
	/// <summary>
	/// An IMAP IDLE context.
	/// </summary>
	/// <remarks>
	/// <para>An IMAP IDLE command does not work like normal commands. Unlike most commands,
	/// the IDLE command does not end until the client sends a separate "DONE" command.</para>
	/// <para>In order to facilitate this, the way this works is that the consumer of MailKit's
	/// IMAP APIs provides a 'doneToken' which signals to the command-processing loop to
	/// send the "DONE" command. Since, like every other IMAP command, it is also necessary to
	/// provide a means of cancelling the IDLE command, it becomes necessary to link the
	/// 'doneToken' and the 'cancellationToken' together.</para>
	/// </remarks>
	sealed class ImapIdleContext : IDisposable
	{
		static readonly byte[] DoneCommand = Encoding.ASCII.GetBytes ("DONE\r\n");
		CancellationTokenRegistration registration;

		/// <summary>
		/// Initializes a new instance of the <see cref="MailKit.Net.Imap.ImapIdleContext"/> class.
		/// </summary>
		/// <remarks>
		/// Creates a new <see cref="MailKit.Net.Imap.ImapIdleContext"/>.
		/// </remarks>
		/// <param name="engine">The IMAP engine.</param>
		/// <param name="doneToken">The done token.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		public ImapIdleContext (ImapEngine engine, CancellationToken doneToken, CancellationToken cancellationToken)
		{
			CancellationToken = cancellationToken;
			DoneToken = doneToken;
			Engine = engine;
		}

		/// <summary>
		/// Get the engine.
		/// </summary>
		/// <remarks>
		/// Gets the engine.
		/// </remarks>
		/// <value>The engine.</value>
		public ImapEngine Engine {
			get; private set;
		}

		/// <summary>
		/// Get the cancellation token.
		/// </summary>
		/// <remarks>
		/// Get the cancellation token.
		/// </remarks>
		/// <value>The cancellation token.</value>
		public CancellationToken CancellationToken {
			get; private set;
		}

		/// <summary>
		/// Get the done token.
		/// </summary>
		/// <remarks>
		/// Gets the done token.
		/// </remarks>
		/// <value>The done token.</value>
		public CancellationToken DoneToken {
			get; private set;
		}

#if false
		/// <summary>
		/// Get whether or not cancellation has been requested.
		/// </summary>
		/// <remarks>
		/// Gets whether or not cancellation has been requested.
		/// </remarks>
		/// <value><c>true</c> if cancellation has been requested; otherwise, <c>false</c>.</value>
		public bool IsCancellationRequested {
			get { return CancellationToken.IsCancellationRequested; }
		}

		/// <summary>
		/// Get whether or not the IDLE command should be ended.
		/// </summary>
		/// <remarks>
		/// Gets whether or not the IDLE command should be ended.
		/// </remarks>
		/// <value><c>true</c> if the IDLE command should end; otherwise, <c>false</c>.</value>
		public bool IsDoneRequested {
			get { return DoneToken.IsCancellationRequested; }
		}
#endif

		void IdleComplete ()
		{
			if (Engine.State == ImapEngineState.Idle) {
				try {
					Engine.Stream.Write (DoneCommand, 0, DoneCommand.Length, CancellationToken);
					Engine.Stream.Flush (CancellationToken);
				} catch {
					return;
				}

				Engine.State = ImapEngineState.Selected;
			}
		}

		/// <summary>
		/// Callback method to be used as the ImapCommand's ContinuationHandler.
		/// </summary>
		/// <remarks>
		/// Callback method to be used as the ImapCommand's ContinuationHandler.
		/// </remarks>
		/// <param name="engine">The ImapEngine.</param>
		/// <param name="ic">The ImapCommand.</param>
		/// <param name="text">The text.</param>
		/// <param name="doAsync"><c>true</c> if the command is being run asynchronously; otherwise, <c>false</c>.</param>
		/// <returns></returns>
		public Task ContinuationHandler (ImapEngine engine, ImapCommand ic, string text, bool doAsync)
		{
			Engine.State = ImapEngineState.Idle;

			registration = DoneToken.Register (IdleComplete);

			return Task.CompletedTask;
		}

		/// <summary>
		/// Releases all resource used by the <see cref="MailKit.Net.Imap.ImapIdleContext"/> object.
		/// </summary>
		/// <remarks>Call <see cref="Dispose"/> when you are finished using the <see cref="MailKit.Net.Imap.ImapIdleContext"/>. The
		/// <see cref="Dispose"/> method leaves the <see cref="MailKit.Net.Imap.ImapIdleContext"/> in an unusable state. After
		/// calling <see cref="Dispose"/>, you must release all references to the
		/// <see cref="MailKit.Net.Imap.ImapIdleContext"/> so the garbage collector can reclaim the memory that the
		/// <see cref="MailKit.Net.Imap.ImapIdleContext"/> was occupying.</remarks>
		public void Dispose ()
		{
			registration.Dispose ();
		}
	}
}
