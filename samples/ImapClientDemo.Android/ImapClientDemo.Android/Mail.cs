using System;
using MailKit;
using MailKit.Net.Imap;

namespace ImapClientDemo
{
    public static class Mail
    {
        static Mail ()
        {
            Client = new ImapClient ();
        }

        public static ImapClient Client { get; set; }

        public static IMailFolder CurrentFolder { get;set; }

        public static MimeKit.MimeMessage CurrentMessage { get; set; }
    }
}

