using System;
using System.Threading;
using System.Threading.Tasks;

using MailKit;
using MailKit.Net.Imap;
using MailKit.Security;

using Microsoft.Identity.Client;

namespace OAuth2ExchangeExample {
    class Program
    {
        const string ExchangeAccount = "username@office365.com";

        public static void Main (string[] args)
        {
            using (var client = new ImapClient ()) {
                client.Connect ("outlook.office365.com", 993, SecureSocketOptions.SslOnConnect);
                if (client.AuthenticationMechanisms.Contains ("OAUTHBEARER") || client.AuthenticationMechanisms.Contains ("XOAUTH2"))
                    AuthenticateAsync (client).GetAwaiter ().GetResult ();
                client.Disconnect (true);
            }
        }

        static async Task AuthenticateAsync (ImapClient client)
        {
            var options = new PublicClientApplicationOptions {
                ClientId = "Application (client) ID",
                TenantId = "Directory (tenant) ID",
                RedirectUri = "https://login.microsoftonline.com/common/oauth2/nativeclient"
            };

            var publicClientApplication = PublicClientApplicationBuilder
                .CreateWithApplicationOptions (options)
                .Build ();

            var scopes = new string[] {
                "email",
                "offline_access",
                "https://outlook.office.com/IMAP.AccessAsUser.All", // Only needed for IMAP
                //"https://outlook.office.com/POP.AccessAsUser.All",  // Only needed for POP
                //"https://outlook.office.com/SMTP.AccessAsUser.All", // Only needed for SMTP
            };

            AuthenticationResult? result;

            try {
                // First, check the cache for an auth token.
                result = await publicClientApplication.AcquireTokenSilent (scopes, username).ExecuteAsync ();
            } catch (MsalUiRequiredException) {
                // If that fails, then try getting an auth token interactively.
                result = await publicClientApplication.AcquireTokenInteractive (scopes).WithLoginHint (username).ExecuteAsync ();
            }

            // Note: We use result.Account.Username here instead of ExchangeAccount because the user *may* have chosen a
            // different Microsoft Exchange account when presented with the browser window during the authentication process.
            SaslMechanism oauth2;

            if (client.AuthenticationMechanisms.Contains ("OAUTHBEARER"))
                oauth2 = new SaslMechanismOAuthBearer (result.Account.Username, result.AccessToken);
            else
                oauth2 = new SaslMechanismOAuth2 (result.Account.Username, result.AccessToken);

            await client.AuthenticateAsync (oauth2);
        }
    }
}
