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

			// Note: For the purposes of this demo, since we have not implemented support for
			// obtaining the user's OAuth2.0 auth_token, we'll have to disable XOAUTH2.
			//
			// OAuth2 is the authentication mechanism that services like GMail are pushing.
			// If you get an exception when trying to log in to your GMail account using this
			// demo, then you probably have not enabled "less secure apps" in your GMail
			// settings. Do not be fooled by Google's labeling of this checkbox, the claim
			// is really only true if the user logs in w/o using SSL (which they enforce).
			Client.AuthenticationMechanisms.Remove ("XOAUTH2");

			try {
				Client.Authenticate (Credentials);
			} catch (Exception ex) {
				MessageBox.Show ("Failed to Authenticate to server. If you are using GMail, then you probably " +
					"need to go into your GMail settings to enable \"less secure apps\" in order " + 
					"to get this demo to work.\n\n" +
					"For a real Mail application, you'll want to add support for obtaining the " +
					"user's OAuth2 credentials to prevent the need for user's to enable this, but " +
					"that is beyond the scope of this demo.",
					"Authentication Error");
			}
		}
	}
}
