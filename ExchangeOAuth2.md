# Using OAuth2 With Exchange (IMAP, POP3 or SMTP)

## Quick Index

* [Register Your Application with Microsoft](#register-your-application-with-microsoft)
* [Authenticating with OAuth2](#authenticating-with-oauth2)
* [Additional Resources](#additional-resources)

## Register Your Application with Microsoft

Go to Microsoft's [Quickstart guide](https://docs.microsoft.com/en-us/azure/active-directory/develop/quickstart-register-app)
for registering an application with the Microsoft identity platform and follow the instructions.

## Authenticating with OAuth2

Now that you have the **Client ID** and **Tenant ID** strings, you'll need to plug those values into
your application.

The following sample code uses the [Microsoft.Identity.Client](https://www.nuget.org/packages/Microsoft.Identity.Client/)
nuget package for obtaining the access token which will be needed by MailKit to pass on to the Exchange
server.

```csharp
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
    //"https://outlook.office.com/SMTP.Send", // Only needed for SMTP
};

var authToken = await publicClientApplication.AcquireTokenInteractive (scopes).ExecuteAsync ();

var oauth2 = new SaslMechanismOAuth2 (authToken.Account.Username, authToken.AccessToken);

using (var client = new ImapClient ()) {
	await client.ConnectAsync ("outlook.office365.com", 993, SecureSocketOptions.SslOnConnect);
	await client.AuthenticateAsync (oauth2);
	await client.DisconnectAsync (true);
}
```

Note: Once you've acquired an auth token using the interactive method above, you can avoid prompting the user
if you cache the `authToken.Account` information and then silently reacquire auth tokens in the future using
the following code:

```csharp
var authToken = await publicClientApplication.AcquireTokenSilent(scopes, account).ExecuteAsync(cancellationToken);
```

## Additional Resources

For more inforrmation, check out the [Microsoft.Identity.Client](https://docs.microsoft.com/en-us/dotnet/api/microsoft.identity.client?view=azure-dotnet)
documentation.
