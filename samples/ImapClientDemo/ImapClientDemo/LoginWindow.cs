using System;
using System.Net;
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
		}

		protected override void OnShown (EventArgs e)
		{
			serverCombo.Text = Program.ImapClientConnection.Host;
			portCombo.Text = Program.ImapClientConnection.Port.ToString ();
			sslCheckbox.Checked = Program.ImapClientConnection.SslOptions == SecureSocketOptions.SslOnConnect;
			loginTextBox.Text = Program.ImapClientConnection.Credentials.UserName;
			passwordTextBox.Text = Program.ImapClientConnection.Credentials.Password;

			base.OnShown (e);
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

		void SignInClicked (object sender, EventArgs e)
		{
			var sslOptions = SecureSocketOptions.StartTlsWhenAvailable;
			var host = serverCombo.Text.Trim ();
			var passwd = passwordTextBox.Text;
			var user = loginTextBox.Text;
			int port = 0;

			if (!string.IsNullOrEmpty (portCombo.Text))
				port = int.Parse (portCombo.Text);

			var credentials = new NetworkCredential (user, passwd);

			if (sslCheckbox.Checked)
				sslOptions = SecureSocketOptions.SslOnConnect;

			Program.ImapClientConnection.Host = host;
			Program.ImapClientConnection.Port = port;
			Program.ImapClientConnection.SslOptions = sslOptions;
			Program.ImapClientConnection.Credentials = credentials;

			Program.MainWindow.LoadContent ();

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
