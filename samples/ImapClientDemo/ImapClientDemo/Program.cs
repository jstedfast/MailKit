using System;
using System.Net;
using System.Linq;
using System.Threading;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows.Forms;

using MailKit;
using MailKit.Net.Imap;
using MailKit.Security;

namespace ImapClientDemo
{
	static class Program
	{
		public static readonly ImapClient Client = new ImapClient (new ProtocolLogger ("imap.txt"));
		public static TaskScheduler GuiTaskScheduler;
		public static SecureSocketOptions SslOptions;
		public static ICredentials Credentials;
		public static MainWindow MainWindow;
		public static Task CurrentTask;
		public static string HostName;
		public static int Port;

		/// <summary>
		/// The main entry point for the application.
		/// </summary>
		[STAThread]
		static void Main ()
		{
			Application.EnableVisualStyles ();
			Application.SetCompatibleTextRenderingDefault (false);

			Client.Disconnected += OnClientDisconnected;

			MainWindow = new MainWindow ();
			
			Application.Run (new LoginWindow ());
		}

		static async void OnClientDisconnected (object sender, EventArgs e)
		{
			await ReconnectAsync ();
		}

		public static async Task ReconnectAsync ()
		{
			// Note: for demo purposes, we're ignoring SSL validation errors (don't do this in production code)
			Client.ServerCertificateValidationCallback = (sender, certificate, chain, sslPolicyErrors) => true;

			await Client.ConnectAsync (HostName, Port, SslOptions);

			try {
				await Client.AuthenticateAsync (Credentials);
			} catch {
				MessageBox.Show ("Failed to Authenticate to server. If you are using GMail, then you probably " +
					"need to go into your GMail settings to enable \"less secure apps\" in order " +
					"to get this demo to work.\n\n" +
					"For a real Mail application, you'll want to add support for obtaining the " +
					"user's OAuth2 credentials to prevent the need for user's to enable this, but " +
					"that is beyond the scope of this demo.",
					"Authentication Error");
				throw;
			}
		}
	}
}
