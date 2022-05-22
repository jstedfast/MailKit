# Using OAuth2 With GMail (IMAP, POP3 or SMTP)

## Quick Index

* [Setting up OAuth2 for use with Google Mail](#setting-up-oauth2-for-use-with-google-mail)
  * [Register Your Application with Google](#register-your-application-with-google)
  * [Obtaining an OAuth2 Client ID and Secret](#obtaining-an-oauth2-client-id-and-secret)
* [Authenticating a Desktop App with the OAuth2 Client ID and Secret](#authenticating-a-desktop-app-with-the-oauth2-client-id-and-secret)
* [Authenticating an ASP.NET Web App with the OAuth2 Client ID and Secret](#authenticating-an-aspnet-web-app-with-the-oauth2-client-id-and-secret)

## Setting up OAuth2 for use with Google Mail

### Register Your Application with Google

Go to [Google's Developer Console](https://cloud.google.com/console).

Click the **Select A Project** button in the **Navigation Bar** at the top of the screen.

![Click "Select A Project"](https://github.com/jstedfast/MailKit/blob/master/Documentation/media/google-developer-console/click-select-a-project.png)

Click the **New Project** button.

![Click "New Project"](https://github.com/jstedfast/MailKit/blob/master/Documentation/media/google-developer-console/click-new-project.png)

Fill in the name **Project Name**, and if appropriate, select the **Organization** that your program
should be associated with. Then click *Create*.

![Create New Project](https://github.com/jstedfast/MailKit/blob/master/Documentation/media/google-developer-console/create-new-project.png)

### Obtaining an OAuth2 Client ID and Secret

Click the **☰** symbol, move down to **APIs & Services** and then select **OAuth consent screen**.

![Click "OAuth consent screen"](https://github.com/jstedfast/MailKit/blob/master/Documentation/media/google-developer-console/click-oauth-consent-screen-menu.png)

Select the **External** radio item and then click **Create**.

![Select "External"](https://github.com/jstedfast/MailKit/blob/master/Documentation/media/google-developer-console/select-external.png)

Fill in the **Application name** and any other fields that are appropriate for your application and then click
**Create**.

![OAuth consent screen](https://github.com/jstedfast/MailKit/blob/master/Documentation/media/google-developer-console/oauth-consent-screen.png)

Click **+ Create Credentials** and then select **OAuth client ID**.

![Click "Create Credentials"](https://github.com/jstedfast/MailKit/blob/master/Documentation/media/google-developer-console/click-create-credentials.png)

Select the **Other** radio item in the **Application type** section and then type in a name to use for the OAuth
client ID. Once completed, click **Create**.

![Select "Other"](https://github.com/jstedfast/MailKit/blob/master/Documentation/media/google-developer-console/select-application-type-other.png)

At this point, you will be presented with a web dialog that will allow you to copy the **Client ID** and
**Client Secret** strings into your clipboard to paste them into your program.

![Client ID and Secret](https://github.com/jstedfast/MailKit/blob/master/Documentation/media/google-developer-console/client-id-and-secret.png)

## Authenticating a Desktop App with the OAuth2 Client ID and Secret

Now that you have the **Client ID** and **Client Secret** strings, you'll need to plug those values into
your application.

The following sample code uses the [Google.Apis.Auth](https://www.nuget.org/packages/Google.Apis.Auth/)
nuget package for obtaining the access token which will be needed by MailKit to pass on to the GMail
server.

```csharp
const string GMailAccount = "username@gmail.com";

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

var oauth2 = new SaslMechanismOAuth2 (credential.UserId, credential.Token.AccessToken);

using (var client = new ImapClient ()) {
	await client.ConnectAsync ("imap.gmail.com", 993, SecureSocketOptions.SslOnConnect);
	await client.AuthenticateAsync (oauth2);
	await client.DisconnectAsync (true);
}
```

## Authenticating an ASP.NET Web App with the OAuth2 Client ID and Secret

Now that you have the **Client ID** and **Client Secret** strings, you'll need to plug those values into
your application.

The following sample code uses the [Google.Apis.Auth](https://www.nuget.org/packages/Google.Apis.Auth/)
nuget package for obtaining the access token which will be needed by MailKit to pass on to the GMail
server.

Add Google Authentication processor to your **Program.cs**.

```csharp
builder.Services.AddAuthentication (options => {
    // This forces challenge results to be handled by Google OpenID Handler, so there's no
    // need to add an AccountController that emits challenges for Login.
    options.DefaultChallengeScheme = GoogleOpenIdConnectDefaults.AuthenticationScheme;
    
    // This forces forbid results to be handled by Google OpenID Handler, which checks if
    // extra scopes are required and does automatic incremental auth.
    options.DefaultForbidScheme = GoogleOpenIdConnectDefaults.AuthenticationScheme;
    
    // Default scheme that will handle everything else.
    // Once a user is authenticated, the OAuth2 token info is stored in cookies.
    options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
})
.AddCookie (options => {
    options.ExpireTimeSpan = TimeSpan.FromMinutes (5);
})
.AddGoogleOpenIdConnect (options => {
    var secrets = GoogleClientSecrets.FromFile ("client_secret.json").Secrets;
    options.ClientId = secrets.ClientId;
    options.ClientSecret = secrets.ClientSecret;
});
```

Ensure that you are using Authorization and HttpsRedirection in your **Program.cs**:

```csharp
app.UseHttpsRedirection ();
app.UseStaticFiles ();
	
app.UseRouting ();

app.UseAuthentication ();
app.UseAuthorization ();
```

Now, using the **GoogleScopedAuthorizeAttribute**, you can request scopes saved in a library as constants and request tokens for these scopes.

```csharp
[GoogleScopedAuthorize(DriveService.ScopeConstants.DriveReadonly)]
public async Task AuthenticateAsync ([FromServices] IGoogleAuthProvider auth)
{
    GoogleCredential? googleCred = await _auth.GetCredentialAsync ();
    string token = await googleCred.UnderlyingCredential.GetAccessTokenForRequestAsync ();
    
    var oauth2 = new SaslMechanismOAuth2 ("UserEmail", token);
    
    using var emailClient = new ImapClient ();
    await emailClient.ConnectAsync ("imap.gmail.com", 993, SecureSocketOptions.SslOnConnect);
    await emailClient.AuthenticateAsync (oauth2);
    await emailClient.DisconnectAsync (true);
}
```

All of that and more has been described in Google's [OAuth 2.0](https://developers.google.com/api-client-library/dotnet/guide/aaa_oauth#web-applications-aspnet-mvc)
documentation. However, be careful since [Asp.Net MVC](https://developers.google.com/api-client-library/dotnet/guide/aaa_oauth#web-applications-asp.net-mvc)
does not work for Asp.Net Core.
