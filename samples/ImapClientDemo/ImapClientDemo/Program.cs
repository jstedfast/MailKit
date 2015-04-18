using System;
using System.Net;
using System.Linq;
using System.Threading;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows.Forms;

using MailKit.Net.Imap;
using MailKit;

namespace ImapClientDemo
{
	static class Program
	{
		public static readonly ImapClient Client = new ImapClient (new ProtocolLogger ("imap.txt"));
		public static ICredentials Credentials;
		public static MainWindow MainWindow;
		public static Uri Uri;

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
			await Reconnect ();
		}

		public static async Task Reconnect ()
		{
			await Client.ConnectAsync (Uri).ConfigureAwait (false);
			Client.Authenticate (Credentials);
		}
	}
}
