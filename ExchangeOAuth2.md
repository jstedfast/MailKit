# Using OAuth2 With Exchange (IMAP, POP3 or SMTP)

## Quick Index

* [Registering Your Application with Microsoft](#registering-your-application-with-microsoft)
* [Configuring the Correct API Permissions for Your Application](#configuring-the-correct-api-permissions-for-your-application)
* Desktop and Mobile Applications
  * [Authenticating a Desktop or Mobile Application with OAuth2](#authenticating-a-desktop-or-mobile-application-with-oauth2)
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

Next, register your the Service Principal for your web service:

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
var confidentialClientApplication = ConfidentialClientApplicationBuilder.Create (clientId)
    .WithAuthority ($"https://login.microsoftonline.com/{tenantId}/v2.0")
    .WithCertificate (certificate) // or .WithClientSecret (clientSecret)
    .Build ();
 
var scopes = new string[] {
    // For IMAP and POP3, use the following scope
    "https://ps.outlook.com/.default"

    // For SMTP, use the following scope
    // "https://outlook.office365.com/.default"
};

var authToken = await confidentialClientApplication.AcquireTokenForClient (scopes).ExecuteAsync ();
var oauth2 = new SaslMechanismOAuth2 (accountEmailAddress, authToken.AccessToken);

using (var client = new ImapClient ()) {
    await client.ConnectAsync ("outlook.office365.com", 993, SecureSocketOptions.SslOnConnect);
    await client.AuthenticateAsync (oauth2);
    await client.DisconnectAsync (true);
}
```

## Additional Resources

For more information, check out the [Microsoft.Identity.Client](https://docs.microsoft.com/en-us/dotnet/api/microsoft.identity.client?view=azure-dotnet)
documentation.
