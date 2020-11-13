using System;
using System.Threading;
using System.Threading.Tasks;

using Google.Apis.Util;
using Google.Apis.Util.Store;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Auth.OAuth2.Flows;

using MailKit;
using MailKit.Net.Imap;
using MailKit.Security;

namespace OAuth2GMailExample {
    class Program
    {
        const string GMailAccount = "username@gmail.com";

        public static void Main (string[] args)
        {
            using (var client = new ImapClient ()) {
                client.Connect ("imap.gmail.com", 993, SecureSocketOptions.SslOnConnect);
                if (client.AuthenticationMechanisms.Contains ("OAUTHBEARER") || client.AuthenticationMechanisms.Contains ("XOAUTH2"))
                    OAuthAsync (client).GetAwaiter ().GetResult ();
                client.Disconnect (true);
            }
        }

        static async Task OAuthAsync (ImapClient client)
        {
            var clientSecrets = new ClientSecrets {
                ClientId = "XXX.apps.googleusercontent.com",
                ClientSecret = "XXX"
            };

            var codeFlow = new GoogleAuthorizationCodeFlow (new GoogleAuthorizationCodeFlow.Initializer {
                DataStore = new FileDataStore ("CredentialCacheFolder", false),
                Scopes = new [] { "https://mail.google.com/" },
                ClientSecrets = clientSecrets
            });

            // Note: For a web app, you'll want to use AuthorizationCodeWebApp instead.
            var codeReceiver = new LocalServerCodeReceiver ();
            var authCode = new AuthorizationCodeInstalledApp (codeFlow, codeReceiver);

            var credential = await authCode.AuthorizeAsync (GMailAccount, CancellationToken.None);

            if (credential.Token.IsExpired (SystemClock.Default))
                await credential.RefreshTokenAsync (CancellationToken.None);

            // Note: We use credential.UserId here instead of GMailAccount because the user *may* have chosen a
            // different GMail account when presented with the browser window during the authentication process.
            SaslMechanism oauth2;

            if (client.AuthenticationMechanisms.Contains ("OAUTHBEARER"))
                oauth2 = new SaslMechanismOAuthBearer (credential.UserId, credential.Token.AccessToken);
            else
                oauth2 = new SaslMechanismOAuth2 (credential.UserId, credential.Token.AccessToken);

            await client.AuthenticateAsync (oauth2);
        }
    }
}
