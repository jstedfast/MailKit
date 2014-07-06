namespace ImapClientDemo
{
	partial class LoginWindow
	{
		/// <summary>
		/// Required designer variable.
		/// </summary>
		private System.ComponentModel.IContainer components = null;

		/// <summary>
		/// Clean up any resources being used.
		/// </summary>
		/// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
		protected override void Dispose (bool disposing)
		{
			if (disposing && (components != null)) {
				components.Dispose ();
			}
			base.Dispose (disposing);
		}

		#region Windows Form Designer generated code

		/// <summary>
		/// Required method for Designer support - do not modify
		/// the contents of this method with the code editor.
		/// </summary>
		private void InitializeComponent ()
		{
			this.loginPanel = new System.Windows.Forms.Panel();
			this.signInButton = new System.Windows.Forms.Button();
			this.passwordTextBox = new System.Windows.Forms.TextBox();
			this.passwordLabel = new System.Windows.Forms.Label();
			this.loginTextBox = new System.Windows.Forms.TextBox();
			this.loginLabel = new System.Windows.Forms.Label();
			this.sslCheckbox = new System.Windows.Forms.CheckBox();
			this.portCombo = new System.Windows.Forms.ComboBox();
			this.portLabel = new System.Windows.Forms.Label();
			this.serverLabel = new System.Windows.Forms.Label();
			this.serverCombo = new System.Windows.Forms.ComboBox();
			this.signInLabel = new System.Windows.Forms.Label();
			this.loginPanel.SuspendLayout();
			this.SuspendLayout();
			// 
			// loginPanel
			// 
			this.loginPanel.BackColor = System.Drawing.Color.White;
			this.loginPanel.Controls.Add(this.signInButton);
			this.loginPanel.Controls.Add(this.passwordTextBox);
			this.loginPanel.Controls.Add(this.passwordLabel);
			this.loginPanel.Controls.Add(this.loginTextBox);
			this.loginPanel.Controls.Add(this.loginLabel);
			this.loginPanel.Controls.Add(this.sslCheckbox);
			this.loginPanel.Controls.Add(this.portCombo);
			this.loginPanel.Controls.Add(this.portLabel);
			this.loginPanel.Controls.Add(this.serverLabel);
			this.loginPanel.Controls.Add(this.serverCombo);
			this.loginPanel.Controls.Add(this.signInLabel);
			this.loginPanel.Location = new System.Drawing.Point(0, 0);
			this.loginPanel.Name = "loginPanel";
			this.loginPanel.Size = new System.Drawing.Size(584, 400);
			this.loginPanel.TabIndex = 0;
			// 
			// signInButton
			// 
			this.signInButton.Enabled = false;
			this.signInButton.Location = new System.Drawing.Point(208, 252);
			this.signInButton.Name = "signInButton";
			this.signInButton.Size = new System.Drawing.Size(251, 25);
			this.signInButton.TabIndex = 11;
			this.signInButton.Text = "Sign In";
			this.signInButton.UseVisualStyleBackColor = true;
			// 
			// passwordTextBox
			// 
			this.passwordTextBox.Location = new System.Drawing.Point(208, 220);
			this.passwordTextBox.MaxLength = 64;
			this.passwordTextBox.Name = "passwordTextBox";
			this.passwordTextBox.PasswordChar = '*';
			this.passwordTextBox.Size = new System.Drawing.Size(251, 25);
			this.passwordTextBox.TabIndex = 10;
			// 
			// passwordLabel
			// 
			this.passwordLabel.Location = new System.Drawing.Point(123, 220);
			this.passwordLabel.Name = "passwordLabel";
			this.passwordLabel.Size = new System.Drawing.Size(78, 25);
			this.passwordLabel.TabIndex = 9;
			this.passwordLabel.Text = "Password:";
			this.passwordLabel.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
			// 
			// loginTextBox
			// 
			this.loginTextBox.Location = new System.Drawing.Point(208, 188);
			this.loginTextBox.MaxLength = 64;
			this.loginTextBox.Name = "loginTextBox";
			this.loginTextBox.Size = new System.Drawing.Size(251, 25);
			this.loginTextBox.TabIndex = 8;
			this.loginTextBox.WordWrap = false;
			// 
			// loginLabel
			// 
			this.loginLabel.Location = new System.Drawing.Point(126, 188);
			this.loginLabel.Name = "loginLabel";
			this.loginLabel.Size = new System.Drawing.Size(75, 25);
			this.loginLabel.TabIndex = 7;
			this.loginLabel.Text = "Login:";
			this.loginLabel.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
			// 
			// sslCheckbox
			// 
			this.sslCheckbox.Location = new System.Drawing.Point(344, 158);
			this.sslCheckbox.Name = "sslCheckbox";
			this.sslCheckbox.Size = new System.Drawing.Size(115, 25);
			this.sslCheckbox.TabIndex = 6;
			this.sslCheckbox.Text = "Enable SSL/TLS";
			this.sslCheckbox.UseVisualStyleBackColor = true;
			// 
			// portCombo
			// 
			this.portCombo.FormattingEnabled = true;
			this.portCombo.Items.AddRange(new object[] {
            "143",
            "993"});
			this.portCombo.Location = new System.Drawing.Point(207, 158);
			this.portCombo.Name = "portCombo";
			this.portCombo.Size = new System.Drawing.Size(131, 25);
			this.portCombo.TabIndex = 5;
			// 
			// portLabel
			// 
			this.portLabel.Location = new System.Drawing.Point(123, 158);
			this.portLabel.Name = "portLabel";
			this.portLabel.Size = new System.Drawing.Size(78, 25);
			this.portLabel.TabIndex = 4;
			this.portLabel.Text = "Port:";
			this.portLabel.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
			// 
			// serverLabel
			// 
			this.serverLabel.Location = new System.Drawing.Point(120, 127);
			this.serverLabel.Name = "serverLabel";
			this.serverLabel.Size = new System.Drawing.Size(81, 25);
			this.serverLabel.TabIndex = 3;
			this.serverLabel.Text = "Server:";
			this.serverLabel.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
			// 
			// serverCombo
			// 
			this.serverCombo.FormattingEnabled = true;
			this.serverCombo.Items.AddRange(new object[] {
            "imap.gmail.com",
            "imap.mail.yahoo.com",
            "imap-mail.outlook.com"});
			this.serverCombo.Location = new System.Drawing.Point(207, 127);
			this.serverCombo.Name = "serverCombo";
			this.serverCombo.Size = new System.Drawing.Size(252, 25);
			this.serverCombo.TabIndex = 2;
			// 
			// signInLabel
			// 
			this.signInLabel.Anchor = System.Windows.Forms.AnchorStyles.None;
			this.signInLabel.BackColor = System.Drawing.Color.WhiteSmoke;
			this.signInLabel.Font = new System.Drawing.Font("Segoe UI", 14.667F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
			this.signInLabel.Location = new System.Drawing.Point(0, 0);
			this.signInLabel.Margin = new System.Windows.Forms.Padding(0);
			this.signInLabel.Name = "signInLabel";
			this.signInLabel.Padding = new System.Windows.Forms.Padding(12);
			this.signInLabel.Size = new System.Drawing.Size(584, 52);
			this.signInLabel.TabIndex = 1;
			this.signInLabel.Text = "Sign In";
			// 
			// LoginWindow
			// 
			this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 17F);
			this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
			this.ClientSize = new System.Drawing.Size(584, 401);
			this.Controls.Add(this.loginPanel);
			this.Font = new System.Drawing.Font("Segoe UI", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
			this.Name = "LoginWindow";
			this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
			this.Text = "MailKit IMAP Demo - Login";
			this.loginPanel.ResumeLayout(false);
			this.loginPanel.PerformLayout();
			this.ResumeLayout(false);

		}

		#endregion

		private System.Windows.Forms.Panel loginPanel;
		private System.Windows.Forms.Label signInLabel;
		private System.Windows.Forms.ComboBox serverCombo;
		private System.Windows.Forms.Label serverLabel;
		private System.Windows.Forms.Label portLabel;
		private System.Windows.Forms.ComboBox portCombo;
		private System.Windows.Forms.CheckBox sslCheckbox;
		private System.Windows.Forms.Label loginLabel;
		private System.Windows.Forms.TextBox loginTextBox;
		private System.Windows.Forms.TextBox passwordTextBox;
		private System.Windows.Forms.Label passwordLabel;
		private System.Windows.Forms.Button signInButton;

	}
}

