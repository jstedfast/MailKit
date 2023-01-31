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
		public static ClientCommandPipeline<ImapClient> ImapCommandPipeline { get; private set; }
		public static ClientConnection<ImapClient> ImapClientConnection { get; set; }

		public static LoginWindow LoginWindow { get; private set; }
		public static MainWindow MainWindow { get; private set; }

		public static SynchronizationContext GuiContext { get; private set; }
		public static TaskScheduler GuiTaskScheduler { get; private set; }
		public static Thread GuiThread { get; private set; }

		/// <summary>
		/// The main entry point for the application.
		/// </summary>
		[STAThread]
		static void Main ()
		{
			Application.EnableVisualStyles ();
			Application.SetCompatibleTextRenderingDefault (false);

			MainWindow = new MainWindow ();
			LoginWindow = new LoginWindow ();

			// Note: SynchronizationContext.Current will be null *until* a Windows.Forms control is instantiated.
			GuiContext = SynchronizationContext.Current;
			GuiTaskScheduler = new CustomTaskScheduler (GuiContext);
			GuiThread = Thread.CurrentThread;

			ImapCommandPipeline = new ClientCommandPipeline<ImapClient> ("IMAP Command Pipeline");
			ImapCommandPipeline.CommandFailed += OnCommandFailed;
			ImapCommandPipeline.ConnectionFailed += OnConnectionFailed;
			ImapCommandPipeline.AuthenticationFailed += OnAuthenticationFailed;
			ImapCommandPipeline.Start ();

			var client = new ImapClient (new ProtocolLogger ("imap.log"));
			var credentials = new NetworkCredential (string.Empty, string.Empty);

			ImapClientConnection = new ClientConnection<ImapClient> (client, "imap.gmail.com", 993, SecureSocketOptions.SslOnConnect, credentials);

			Application.Run (LoginWindow);
		}

		static void OnConnectionFailed (object state)
		{
			var e = (ConnectionFailedEventArgs<ImapClient>) state;

			MessageBox.Show (MainWindow, e.Exception.Message, $"Failed to connect to {e.Connection.Host}:{e.Connection.Port}");
			LoginWindow.Visible = true;
			MainWindow.Visible = false;
		}

		static void OnConnectionFailed (object sender, ConnectionFailedEventArgs<ImapClient> e)
		{
			// This event is raised by the ImapClient and will be running in the IMAP Command Pipeline thread. Defer this back to the GUI thread.
			GuiContext.Send (OnConnectionFailed, e);
		}

		static void OnAuthenticationFailed (object state)
		{
			var e = (AuthenticationFailedEventArgs<ImapClient>) state;
			string text;

			if (e.Connection.Credentials.UserName.EndsWith ("@gmail.com", StringComparison.OrdinalIgnoreCase)) {
				text = "You probably need to go into your GMail settings to enable \"less secure apps\" in order " +
					"to get this demo to work.\n\nFor a real Mail application, you'll want to add support for " +
					"obtaining the user's OAuth2 credentials to prevent the need for user's to enable this, but " +
					"that is beyond the scope of this demo.";
			} else {
				text = e.Exception.Message;
			}

			MessageBox.Show (MainWindow, text, $"Failed to authenticate {e.Connection.Credentials.UserName}");
			LoginWindow.Visible = true;
			MainWindow.Visible = false;
		}

		static void OnAuthenticationFailed (object sender, AuthenticationFailedEventArgs<ImapClient> e)
		{
			// This event is raised by the ImapClient and will be running in the IMAP Command Pipeline thread. Defer this back to the GUI thread.
			GuiContext.Send (OnAuthenticationFailed, e);
		}

		static void OnCommandFailed (object state)
		{
			var e = (CommandFailedEventArgs) state;

			MessageBox.Show (MainWindow, e.Exception.Message, "Failed to send command.");
		}

		static void OnCommandFailed (object sender, CommandFailedEventArgs e)
		{
			// This event is raised by the ImapClient and will be running in the IMAP Command Pipeline thread. Defer this back to the GUI thread.
			GuiContext.Send (OnCommandFailed, e);
		}

		delegate void InvokeOnMainThreadDelegate ();

		public static void RunOnMainThread (Control control, Action action)
		{
			control.Invoke (new InvokeOnMainThreadDelegate (action));
		}
	}
}
