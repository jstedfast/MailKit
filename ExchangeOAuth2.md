# Using OAuth2 With Exchange (IMAP, POP3 or SMTP)

## Quick Index

* [Registering Your Application with Microsoft](#registering-your-application-with-microsoft)
* [Configuring the Correct API Permissions for Your Application](#configuring-the-correct-api-permissions-for-your-application)
* Desktop and Mobile Applications
  * [Authenticating a Desktop or Mobile Application with OAuth2](#authenticating-a-desktop-or-mobile-application-with-oauth2)
* Web Applications
  * [Authenticating a Web Application with OAuth2](#authenticating-a-web-application-with-oauth2)
* Web Services
  * [Registering Service Principals for Your Web Service](#registering-service-principals-for-your-web-service)
  * [Granting Permissions for Your Web Service](#granting-permissions-for-your-web-service)
  * [Authenticating a Web Service with OAuth2](#authenticating-a-web-service-with-oauth2)
* [Additional Resources](#additional-resources)

## Registering Your Application with Microsoft

Whether you are writing a Desktop, Mobile or Web Service application, the first thing you'll need to do is register your
application with Microsoft's Identity Platform. To do this, go to Microsoft's
[Quickstart guide](https://docs.microsoft.com/en-us/azure/active-directory/develop/quickstart-register-app)
and follow the instructions.

## Configuring the Correct API Permissions for Your Application

There are several different API permissions that you may want to configure depending on which protocols your application intends to use.

Follow the instructions for [adding the POP, IMAP, and/or SMTP permissions to your Entra AD application](https://learn.microsoft.com/en-us/exchange/client-developer/legacy-protocols/how-to-authenticate-an-imap-pop-smtp-application-by-using-oauth#use-client-credentials-grant-flow-to-authenticate-smtp-imap-and-pop-connections).

## Desktop and Mobile Applications

### Authenticating a Desktop or Mobile Application with OAuth2

Now that you have the **Client ID** and **Tenant ID** strings, you'll need to plug those values into
your application.

The following sample code uses the [Microsoft.Identity.Client](https://www.nuget.org/packages/Microsoft.Identity.Client/)
nuget package for obtaining the access token which will be needed by MailKit to pass on to the Exchange
server.

```csharp
static async Task<AuthenticationResult> GetPublicClientOAuth2CredentialsAsync (string protocol, string emailAddress, CancellationToken cancellationToken = default)
{
    var options = new PublicClientApplicationOptions {
        ClientId = "Application (client) ID",
        TenantId = "Directory (tenant) ID",

        // Use "https://login.microsoftonline.com/common/oauth2/nativeclient" for apps using
        // embedded browsers or "http://localhost" for apps that use system browsers.
        RedirectUri = "https://login.microsoftonline.com/common/oauth2/nativeclient"
    };
 
    var publicClientApplication = PublicClientApplicationBuilder
        .CreateWithApplicationOptions (options)
        .Build ();

    string[] scopes;
 
    if (protocol.Equals ("IMAP", StringComparison.OrdinalIgnoreCase)) {
        scopes = new string[] {
            "email",
            "offline_access",
            "https://outlook.office.com/IMAP.AccessAsUser.All"
        };
    } else if (protocol.Equals ("POP", StringComparison.OrdinalIgnoreCase)) {
        scopes = new string[] {
            "email",
            "offline_access",
            "https://outlook.office.com/POP.AccessAsUser.All"
        };
    } else {
        scopes = new string[] {
            "email",
            "offline_access",
            "https://outlook.office.com/SMTP.Send"
        };
    }

    try {
        // First, check the cache for an auth token.
        return await publicClientApplication.AcquireTokenSilent (scopes, emailAddress).ExecuteAsync (cancellationToken);
    } catch (MsalUiRequiredException) {
        // If that fails, then try getting an auth token interactively.
        return await publicClientApplication.AcquireTokenInteractive (scopes).WithLoginHint (emailAddress).ExecuteAsync (cancellationToken);
    }
}
```

#### IMAP (using PublicClientApplication)

```csharp
var result = await GetPublicClientOAuth2CredentialsAsync ("IMAP", "username@outlook.com");

// Note: We always use result.Account.Username instead of `Username` because the user may have selected an alternative account.
var oauth2 = new SaslMechanismOAuth2 (result.Account.Username, result.AccessToken);

using (var client = new ImapClient ()) {
    await client.ConnectAsync ("outlook.office365.com", 993, SecureSocketOptions.SslOnConnect);
    await client.AuthenticateAsync (oauth2);
    await client.DisconnectAsync (true);
}
```

#### SMTP (using PublicClientApplication)

```csharp
var result = await GetPublicClientOAuth2CredentialsAsync ("SMTP", "username@outlook.com");

// Note: We always use result.Account.Username instead of `Username` because the user may have selected an alternative account.
var oauth2 = new SaslMechanismOAuth2 (result.Account.Username, result.AccessToken);

using (var client = new SmtpClient ()) {
    await client.ConnectAsync ("smtp.office365.com", 587, SecureSocketOptions.StartTls);
    await client.AuthenticateAsync (oauth2);
    await client.DisconnectAsync (true);
}
```

Note: Once you've acquired an auth token using the interactive method above, you can avoid prompting the user
if you cache the `result.Account` information and then silently reacquire auth tokens in the future using
the following code:

```csharp
var result = await publicClientApplication.AcquireTokenSilent(scopes, account).ExecuteAsync(cancellationToken);
```

Note: for information on caching tokens, see Microsoft's documentation about how to implement a
[cross-platform token cache](https://github.com/AzureAD/microsoft-authentication-extensions-for-dotnet/wiki/Cross-platform-Token-Cache).

## Web Applications

### Authenticating a Web Application with OAuth2

Use this if you want to send/receive mail on behalf of a user.

```csharp
// Common Code
using Microsoft.Graph;
using Microsoft.Identity.Client;
using Microsoft.Kiota.Abstractions.Authentication;

public static class OAuthMicrosoft
{
    public static readonly string[] RegistrationScopes = new string[] {
        "offline_access",
        "User.Read",
        "Mail.Send",
        "https://outlook.office.com/SMTP.Send",
        "https://outlook.office.com/IMAP.AccessAsUser.All",
    };

    public static readonly string[] SmtpScopes = new string[] {
        "email",
        "offline_access",
        "https://outlook.office.com/SMTP.Send"
    };

    public static readonly string[] ImapScopes = new string[] {
        "email",
        "offline_access",
        "https://outlook.office.com/IMAP.AccessAsUser.All",
    };

    public static IConfidentialClientApplication CreateConfidentialClient ()
    {
        var clientId = "Application (client) ID";
        var tenantId = "common"; // common = anybody with microsoft account personal or organization; other options see https://learn.microsoft.com/en-us/entra/identity-platform/v2-protocols#endpoints
        var clientSecret = "client secret";

        var redirectURL = "https://example.com/oauth/microsoft/callback";

        var confidentialClientApplication = ConfidentialClientApplicationBuilder.Create (clientId)
            .WithAuthority ($"https://login.microsoftonline.com/{tenantId}/v2.0")
            .WithClientSecret (clientSecret)
            .WithRedirectUri (redirectURL)
            .Build ();

        // You also need to configure an MSAL token cache. so that token are remembered.
        return confidentialClientApplication;
    }
}
```

```csharp
// Registration page - redirect user to Microsoft to get authorization 
public async Task<IActionResult> OnPostAsync ()
{
    var client = OAuthMicrosoft.CreateConfidentialClient ();

    // Note: When getting authorization, specify all of the scopes that your application will ever need (eg. SMTP /and/ IMAP).
    // Later, when requesting an access token, you will only ask for the specific scopes that you need (e.g. SMTP).
    var authurlbuilder = client.GetAuthorizationRequestUrl (OAuthMicrosoft.RegistrationScopes);
    var authurl = await authurlbuilder.ExecuteAsync ();

    return this.Redirect (authurl.ToString ());
}

// Callback page = https://example.com/oauth/microsoft/callback in this example
public async Task<IActionResult> OnGet ([FromQuery] string code)
{
    var confidentialClientApplication = OAuthMicrosoft.CreateConfidentialClient ();
    var scopes = OAuthMicrosoft.SmtpScopes;

    var auth = await confidentialClientApplication.AcquireTokenByAuthorizationCode (scopes, code).ExecuteAsync (); //this saves the token in msal cache

    var ident = auth.Account.HomeAccountId.Identifier;
    // Note: you will need to persist the ident to refer to later.
}

// Use the credentials

public async Task SendEmailAsync (string ident)
{
    var confidentialClientApplication = OAuthMicrosoft.CreateConfidentialClient ();
    var account = await confidentialClientApplication.GetAccountAsync (ident);
    var scopes = OAuthMicrosoft.SmtpScopes;

    try {
        var auth = await confidentialClientApplication.AcquireTokenSilent (scopes, account).ExecuteAsync ();

        using (var client = new SmtpClient ()) {
            await client.ConnectAsync ("smtp-mail.outlook.com", 587, SecureSocketOptions.StartTls);

            var oauth2 = new SaslMechanismOAuth2 (auth.Account.Username, auth.AccessToken);

            await client.AuthenticateAsync (oauth2);

            var serverfeedback = await client.SendAsync (message);
            await client.DisconnectAsync (true);
        }
    } catch (MsalUiRequiredException) {
        throw new Exception ("Need to get authorization again");
    }
}

public async Task TestImapAsync (string ident)
{
    var confidentialClientApplication = OAuthMicrosoft.CreateConfidentialClient ();
    var account = await confidentialClientApplication.GetAccountAsync (ident);
    var scopes = OAuthMicrosoft.ImapScopes;

    var auth = await confidentialClientApplication.AcquireTokenSilent (scopes, account).ExecuteAsync ();    

    var oauth2 = new SaslMechanismOAuth2 (auth.Account.Username, auth.AccessToken);

    using (var client = new ImapClient ()) {
        await client.ConnectAsync ("outlook.office365.com", 993, SecureSocketOptions.SslOnConnect);
        await client.AuthenticateAsync (oauth2);
        await client.DisconnectAsync (true);
    }
}
```

## Web Services

### Registering Service Principals for Your Web Service

Once your web service has been registered, the tenant admin will need to register your service principal.

To use the New-ServicePrincipal cmdlet, open an [Azure Powershell](https://learn.microsoft.com/en-us/powershell/azure/new-azureps-module-az?view=azps-10.2.0)
terminal and install ExchangeOnlineManagement and connect to your tenant as shown below:

```powershell
Install-Module -Name ExchangeOnlineManagement -allowprerelease
Import-module ExchangeOnlineManagement 
Connect-ExchangeOnline -Organization <tenantId>
```

Next, register the Service Principal for your web service:

```powershell
New-ServicePrincipal -AppId <APPLICATION_ID> -ObjectId <OBJECT_ID> [-Organization <ORGANIZATION_ID>]
```

### Granting Permissions for Your Web Service

In order to grant permissions for your web service to access an Office365 and/or Exchange account, you'll need to first get the
Service Principal ID registered in the previous step using the following command:

```powershell
Get-ServicePrincipal | fl
```

Once you have the Service Principal ID for your web service, use the following command to add full
mailbox permissions for the email account that your web service will be accessing:

```powershelllo;.k,;
Add-MailboxPermission -Identity "john.smith@example.com" -User 
<SERVICE_PRINCIPAL_ID> -AccessRights FullAccess
```

### Authenticating a Web Service with OAuth2

Now that you have the **Client ID** and **Tenant ID** strings, you'll need to plug those values into
your application.

The following sample code uses the [Microsoft.Identity.Client](https://www.nuget.org/packages/Microsoft.Identity.Client/)
nuget package for obtaining the access token which will be needed by MailKit to pass on to the Exchange
server.

```csharp
static async Task<AuthenticationResult> GetConfidentialClientOAuth2CredentialsAsync (string protocol, CancellationToken cancellationToken = default)
{
    var confidentialClientApplication = ConfidentialClientApplicationBuilder.Create (clientId)
        .WithAuthority ($"https://login.microsoftonline.com/{tenantId}/v2.0")
        .WithCertificate (certificate) // or .WithClientSecret (clientSecret)
        .Build ();

    string[] scopes;

    if (protocol.Equals ("SMTP", StringComparison.OrdinalIgnoreCase)) {
        scopes = new string[] {
            // For SMTP, use the following scope
            "https://outlook.office365.com/.default"
        };
    } else {
        scopes = new string[] {
            // For IMAP and POP3, use the following scope
            "https://ps.outlook.com/.default"
        };
    }

    return await confidentialClientApplication.AcquireTokenForClient (scopes).ExecuteAsync (cancellationToken);
}
```

#### IMAP (using ConfidentialClientApplication)

```csharp
var result = await GetConfidentialClientOAuth2CredentialsAsync ("IMAP");
var oauth2 = new SaslMechanismOAuth2 ("username@outlook.com", result.AccessToken);

using (var client = new ImapClient ()) {
    await client.ConnectAsync ("outlook.office365.com", 993, SecureSocketOptions.SslOnConnect);
    await client.AuthenticateAsync (oauth2);
    await client.DisconnectAsync (true);
}
```

#### SMTP (using ConfidentialClientApplication)

```csharp
var result = await GetConfidentialClientOAuth2CredentialsAsync ("SMTP");
var oauth2 = new SaslMechanismOAuth2 ("username@outlook.com", result.AccessToken);

using (var client = new SmtpClient ()) {
    await client.ConnectAsync ("smtp.office365.com", 587, SecureSocketOptions.StartTls);
    await client.AuthenticateAsync (oauth2);
    await client.DisconnectAsync (true);
}
```

## Additional Resources

For more information, check out the [Microsoft.Identity.Client](https://docs.microsoft.com/en-us/dotnet/api/microsoft.identity.client?view=azure-dotnet)
documentation.
