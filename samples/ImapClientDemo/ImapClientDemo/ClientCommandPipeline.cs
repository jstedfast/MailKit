using System;
using System.Threading;
using System.Collections.Concurrent;

using MailKit;

namespace ImapClientDemo
{
	class ClientCommandPipeline<T> where T : IMailService
	{
		readonly ConcurrentQueue<ClientCommand<T>> queue;
		readonly CancellationTokenSource cancellation;
		readonly ManualResetEvent resetEvent;
		readonly Thread thread;

		public ClientCommandPipeline (string name)
		{
			cancellation = new CancellationTokenSource ();
			queue = new ConcurrentQueue<ClientCommand<T>> ();
			resetEvent = new ManualResetEvent (false);
			thread = new Thread (MainLoop) {
				Name = name,
				IsBackground = true,
			};
		}

		public void Start ()
		{
			if (thread.ThreadState.HasFlag (ThreadState.Unstarted))
				thread.Start ();
		}

		public void Stop ()
		{
			if (!thread.ThreadState.HasFlag (ThreadState.Running))
				return;

			cancellation.Cancel ();
			resetEvent.Set ();
			thread.Abort ();
		}

		public void Enqueue (ClientCommand<T> command)
		{
			queue.Enqueue (command);
			resetEvent.Set ();
		}

		public event EventHandler<ConnectionFailedEventArgs<T>> ConnectionFailed;

		public event EventHandler<AuthenticationFailedEventArgs<T>> AuthenticationFailed;

		public event EventHandler<CommandFailedEventArgs> CommandFailed;

		void MainLoop ()
		{
			while (!cancellation.IsCancellationRequested) {
				if (queue.TryDequeue (out var command)) {
					try {
						command.Connection.EnsureConnected (cancellation.Token);
					} catch (OperationCanceledException) {
						break;
					} catch (Exception ex) {
						ConnectionFailed?.Invoke (this, new ConnectionFailedEventArgs<T> (command.Connection, ex));
						continue;
					}

					try {
						command.Connection.EnsureAuthenticated (cancellation.Token);
					} catch (OperationCanceledException) {
						break;
					} catch (Exception ex) {
						AuthenticationFailed?.Invoke (this, new AuthenticationFailedEventArgs<T> (command.Connection, ex));
						continue;
					}

					try {
						command.Run (cancellation.Token);
					} catch (OperationCanceledException) {
						break;
					} catch (Exception ex) {
						CommandFailed?.Invoke (this, new CommandFailedEventArgs (ex));
						continue;
					}
				} else {
					resetEvent.WaitOne ();
					resetEvent.Reset ();
				}
			}
		}
	}
}
