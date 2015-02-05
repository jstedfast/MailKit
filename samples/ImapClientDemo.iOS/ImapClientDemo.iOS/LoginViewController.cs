
using System;
using System.Collections.Generic;
using System.Linq;

using MonoTouch.Dialog;

using Foundation;
using UIKit;

namespace ImapClientDemo.iOS
{
    public partial class LoginViewController : DialogViewController
    {
        FoldersViewController foldersViewController;

        public LoginViewController () : base (UITableViewStyle.Grouped, null)
        {
            Root = new RootElement ("IMAP Login") {
                new Section ("Server") {
                    new EntryElement ("Host", "imap.gmail.com", ""),
                    new EntryElement ("Port", "993", "") {
                        KeyboardType = UIKeyboardType.NumberPad
                    },
                    new CheckboxElement ("Use SSL", false)
                },
                new Section ("Account") {
                    new EntryElement ("Username", "Email / Username", ""),
                    new EntryElement ("Password", "password", "", true)
                },
                new Section () {
                    new StyledStringElement ("Login", Login)
                }
            };
        }

        async void Login ()
        {
            var host = (Root [0] [0] as EntryElement);
            var port = (Root [0] [1] as EntryElement);
            var ssl = (Root [0] [2] as CheckboxElement);
            var username = (Root [1] [0] as EntryElement);
            var password = (Root [1] [1] as EntryElement);

            host.FetchValue ();
            port.FetchValue ();
            username.FetchValue ();
            password.FetchValue ();

            int iport = 0;
            int.TryParse (port.Value, out iport);

            try {
                // Connect to server
                await Mail.Client.ConnectAsync (host.Value, iport, ssl.Value);

                // Remove this auth mechanism since we don't have an oauth token
                Mail.Client.AuthenticationMechanisms.Remove ("XOAUTH");

                try {
                    // Authenticate now that we're connected
                    await Mail.Client.AuthenticateAsync (username.Value, password.Value);

                    // Show the folders view controller
                    foldersViewController = new FoldersViewController();
                    NavigationController.PushViewController (foldersViewController, true);

                } catch (Exception aex) {
                    Console.WriteLine (aex);
                    Mail.MessageBox ("Authentication Error", "Failed to Authenticate to server");
                }
            } catch (Exception cex) {
                Console.WriteLine (cex);
                Mail.MessageBox ("Connection Error", "Failed to connect to server");
            }
        }
    }
}
