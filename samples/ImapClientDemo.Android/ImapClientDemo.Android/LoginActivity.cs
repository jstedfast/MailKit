
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Android.App;
using Android.Content;
using Android.OS;
using Android.Runtime;
using Android.Views;
using Android.Widget;

namespace ImapClientDemo
{
    [Activity (Label = "Login")]			
    public class LoginActivity : Activity
    {
        TextView textServer;
        TextView textPort;
        TextView textLogin;
        TextView textPassword;
        CheckBox checkSsl;
        Button buttonLogin;

        protected override void OnCreate (Bundle bundle)
        {
            base.OnCreate (bundle);

            SetContentView (Resource.Layout.LoginLayout);

            textServer = FindViewById<TextView> (Resource.Id.textServer);
            textPort = FindViewById<TextView> (Resource.Id.textPort);
            textLogin = FindViewById<TextView> (Resource.Id.textLogin);
            textPassword = FindViewById<TextView> (Resource.Id.textPassword);
            checkSsl = FindViewById<CheckBox> (Resource.Id.checkSsl);
            buttonLogin = FindViewById<Button> (Resource.Id.buttonLogin);

            buttonLogin.Click += buttonLogin_Click;
        }

        async void buttonLogin_Click (object sender, EventArgs e)
        {
            buttonLogin.Enabled = false;

            int port = 0;
            int.TryParse (textPort.Text, out port);

            try {
                await Mail.Client.ConnectAsync (textServer.Text, port, checkSsl.Checked);

                Mail.Client.AuthenticationMechanisms.Remove ("XOAUTH2");

                try { 
                    await Mail.Client.AuthenticateAsync (textLogin.Text, textPassword.Text); 

                    StartActivity (typeof (FoldersActivity));
                }
                catch {
                    Toast.MakeText (this, "Failed to Authenticate!", ToastLength.Short).Show ();
                }
            } catch (Exception ex) {
                Console.WriteLine (ex);
                Toast.MakeText (this, "Failed to Connect!", ToastLength.Short).Show ();
            }

            buttonLogin.Enabled = true;
        }
    }
}

