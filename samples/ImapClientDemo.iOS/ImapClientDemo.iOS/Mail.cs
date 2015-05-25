using System;

using UIKit;

using MailKit;
using MailKit.Net.Imap;

namespace ImapClientDemo.iOS
{
    public static class Mail
    {
        static Mail ()
        {
            Client = new ImapClient ();
        }

        public static ImapClient Client { get; set; }

        public static void MessageBox (string title, string message)
        {
            var av = new UIAlertView (title, message, null, "OK");
            av.Show ();
        }
    }
}
