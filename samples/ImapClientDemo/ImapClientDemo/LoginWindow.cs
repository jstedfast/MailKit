using System;
using System.Net;
using System.Threading;
using System.Windows.Forms;

using MailKit.Security;

namespace ImapClientDemo
{
	public partial class LoginWindow : Form
	{
		public LoginWindow ()
		{
			InitializeComponent ();

			sslCheckbox.CheckedChanged += EnableSSLChanged;
			passwordTextBox.TextChanged += LoginChanged;
			loginTextBox.TextChanged += LoginChanged;
			serverCombo.TextChanged += ServerChanged;
			portCombo.TextChanged += PortChanged;
			signInButton.Click += SignInClicked;

			serverCombo.Text = "imap.gmail.com";
		}

		void CheckCanLogin ()
		{
			signInButton.Enabled = !string.IsNullOrEmpty (serverCombo.Text) &&
				!string.IsNullOrEmpty (loginTextBox.Text) &&
				!string.IsNullOrEmpty (passwordTextBox.Text);
		}

		void PortChanged (object sender, EventArgs e)
		{
			var port = portCombo.Text.Trim ();

			switch (port) {
			case "143":
				sslCheckbox.Checked = false;
				break;
			case "993":
				sslCheckbox.Checked = true;
				break;
			}
		}

		void ServerChanged (object sender, EventArgs e)
		{
			switch (serverCombo.Text) {
			case "imap.gmail.com":
			case "imap.mail.yahoo.com":
			case "imap-mail.outlook.com":
				sslCheckbox.Checked = true;
				portCombo.Text = "993";
				break;
			}

			CheckCanLogin ();
		}

		void EnableSSLChanged (object sender, EventArgs e)
		{
			var checkbox = (CheckBox) sender;
			var port = portCombo.Text;

			if (string.IsNullOrEmpty (port))
				portCombo.Text = checkbox.Checked ? "993" : "143";
		}

		void LoginChanged (object sender, EventArgs e)
		{
			CheckCanLogin ();
		}

		async void SignInClicked (object sender, EventArgs e)
		{
			var options = SecureSocketOptions.StartTlsWhenAvailable;
			var host = serverCombo.Text.Trim ();
			var passwd = passwordTextBox.Text;
			var user = loginTextBox.Text;
			int port;

			if (!string.IsNullOrEmpty (portCombo.Text))
				port = int.Parse (portCombo.Text);
			else
				port = 0; // default

			Program.Credentials = new NetworkCredential (user, passwd);

			if (sslCheckbox.Checked)
				options = SecureSocketOptions.SslOnConnect;

			try {
				await Program.ReconnectAsync (host, port, options);
			} catch {
				MessageBox.Show ("Failed to Authenticate to server. If you are using GMail, then you probably " +
					"need to go into your GMail settings to enable \"less secure apps\" in order " +
					"to get this demo to work.\n\n" +
					"For a real Mail application, you'll want to add support for obtaining the " +
					"user's OAuth2 credentials to prevent the need for user's to enable this, but " +
					"that is beyond the scope of this demo.",
					"Authentication Error");
				return;
			}

			Program.Queue (Program.MainWindow.LoadContentAsync);

			Program.MainWindow.Visible = true;
			Visible = false;
		}

		protected override void OnClosed (EventArgs e)
		{
			base.OnClosed (e);

			Application.Exit ();
		}
	}
}
