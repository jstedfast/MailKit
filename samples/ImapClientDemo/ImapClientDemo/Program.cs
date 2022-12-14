using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

using MailKit;
using MailKit.Net.Imap;
using MailKit.Security;

namespace ImapClientDemo
{
	static class Program
	{
		public static SynchronizationContext GuiContext { get; private set; }
		public static ImapClient Client { get; private set; }
		public static ICredentials Credentials;
		public static MainWindow MainWindow;

		static CustomTaskScheduler GuiTaskScheduler;
		static Task CurrentTask;

		/// <summary>
		/// The main entry point for the application.
		/// </summary>
		[STAThread]
		static void Main ()
		{
			Application.EnableVisualStyles ();
			Application.SetCompatibleTextRenderingDefault (false);

			Client = new ImapClient (new ProtocolLogger ("imap.txt"));
			Client.Disconnected += OnClientDisconnected;

			MainWindow = new MainWindow ();

			// Note: SynchronizationContext.Current will be null *until* a Windows.Forms control is instantiated.
			GuiContext = SynchronizationContext.Current;
			GuiTaskScheduler = new CustomTaskScheduler (GuiContext);
			CurrentTask = Task.CompletedTask;

			Application.Run (new LoginWindow ());
		}

		class AsyncTaskProxy
		{
			readonly Func<Task> action;

			public AsyncTaskProxy (Func<Task> action)
			{
				this.action = action;
			}

			public async Task Run (Task task)
			{
				await task;
				await action ();
			}
		}

		public static void Queue (Func<Task> action)
		{
			var proxy = new AsyncTaskProxy (action);

			Queue (proxy.Run);
		}

		public static void Queue (Func<Task, Task> action)
		{
			CurrentTask = CurrentTask.ContinueWith (action, GuiTaskScheduler);
		}

		public static void Queue (Func<Task, object, Task> action, object state)
		{
			CurrentTask = CurrentTask.ContinueWith (action, state, GuiTaskScheduler);
		}

		static void OnClientDisconnected (object sender, DisconnectedEventArgs e)
		{
			if (e.IsRequested)
				return;

			Queue (ReconnectAsync, e);
		}

		static Task ReconnectAsync (Task task, object state)
		{
			var e = (DisconnectedEventArgs) state;

			return ReconnectAsync (e.Host, e.Port, e.Options);
		}

		public static async Task ReconnectAsync (string host, int port, SecureSocketOptions options)
		{
			// Note: for demo purposes, we're ignoring SSL validation errors (don't do this in production code)
			Client.ServerCertificateValidationCallback = (sender, certificate, chain, sslPolicyErrors) => true;

			await Client.ConnectAsync (host, port, options);

			await Client.AuthenticateAsync (Credentials);

			if (Client.Capabilities.HasFlag (ImapCapabilities.UTF8Accept))
				await Client.EnableUTF8Async ();

			CurrentTask = Task.CompletedTask;
		}
	}
}
