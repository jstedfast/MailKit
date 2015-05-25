//
// LoginActivity.cs
//
// Author: Jeffrey Stedfast <jeff@xamarin.com>
//
// Copyright (c) 2013-2015 Xamarin Inc. (www.xamarin.com)
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
// THE SOFTWARE.
//

using System;

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
			int port;

            buttonLogin.Enabled = false;

            int.TryParse (textPort.Text, out port);

            try {
                await Mail.Client.ConnectAsync (textServer.Text, port, checkSsl.Checked);

                Mail.Client.AuthenticationMechanisms.Remove ("XOAUTH2");

                try { 
                    await Mail.Client.AuthenticateAsync (textLogin.Text, textPassword.Text); 

                    StartActivity (typeof (FoldersActivity));
                } catch {
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
