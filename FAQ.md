## Frequently Asked Questions

### GMail isn't showing me all of my POP3 or IMAP messages. Is this a bug in MailKit?

No, this is just a problem with your GMail POP3 and/or IMAP settings. By default, GMail's POP3
and IMAP server does not behave like standard POP3 or IMAP servers and hides messages from
clients using those protocols (as well as having other non-standard behavior).

If you want to configure your GMail POP3 or IMAP settings to behave the way POP3 and IMAP are
intended to behave according to their protocol specifications, you'll need to log in to your
GMail account via your web browser and navigate to the `Forwarding and POP/IMAP` tab of your
GMail Settings page and set your options to look like this:

![GMail POP3 and IMAP Settings](http://content.screencast.com/users/jeff.xamarin/folders/Jing/media/7d50dada-6cb0-4ab1-b117-8600fb5e07d4/00000022.png "GMail POP3 and IMAP Settings")

### How can I log in to a GMail account using OAuth 2.0?

The first thing you need to do is follow [Google's instructions](https://developers.google.com/accounts/docs/OAuth2) 
for obtaining OAuth 2.0 credentials for your application.

Once you've done that, the easiest way to obtain an access token is to use Google's [Google.Apis.Auth](https://www.nuget.org/packages/Google.Apis.Auth/) library:

```csharp
var certificate = new X509Certificate2 (@"C:\path\to\certificate.p12", "password", X509KeyStorageFlags.Exportable);
var credential = new ServiceAccountCredential (new ServiceAccountCredential.Initializer ("your-developer-id@developer.gserviceaccount.com") {
    // Note: other scopes can be found here: https://developers.google.com/gmail/api/auth/scopes
    Scopes = new[] { "https://mail.google.com/" },
    User = "username@gmail.com"
}.FromCertificate (certificate));

bool result = await credential.RequestAccessTokenAsync (cancel.Token);

// Note: result will be true if the access token was received successfully
```

Now that you have an access token (`credential.Token.AccessToken`), you can use it with MailKit as if it were
the password:

```csharp
using (var client = new ImapClient ()) {
    client.Connect ("imap.gmail.com", 993, true);
    
    // use the access token as the password string
    client.Authenticate ("username@gmail.com", credential.Token.AccessToken);
}
```

### How can I search for messages delivered between two dates?

The obvious solution is:

```csharp
var query = SearchQuery.DeliveredAfter (dateRange.BeginDate).And (SearchQuery.DeliveredBefore (dateRange.EndDate));
var results = folder.Search (query);
```

However, it has been reported to me that this doesn't work reliably depending on the IMAP server implementation.

If you find that this query doesn't get the expected results for your IMAP server, here's another solution that should always work:

```csharp
var query = SearchQuery.Not (SearchQuery.DeliveredBefore (dateRange.BeginDate).Or (SearchQuery.DeliveredAfter (dateRange.EndDate)));
var results = folder.Search (query);
```
