# Frequently Asked Questions

## Question Index

* [How can I get a protocol log for IMAP, POP3, or SMTP to see what is going wrong?](#ProtocolLog)
* [Why doesn't MailKit find some of my GMail POP3 or IMAP messages?](#GMailHiddenMessages)
* [How can I log in to a GMail account using OAuth 2.0?](#GMailOAuth2)
* [How can I search for messages delivered between two dates?](#SearchBetween2Dates)
* [What does "The ImapClient is currently busy processing a command." mean?](#ImapClientBusy)
* [Why do I get InvalidOperationException: "The folder is not currently open."?](#FolderNotOpenException)
* [ImapFolder.MoveTo() does not move the message out of the source folder. Why not?](#MoveDoesNotMove)
* [How can I mark messages as read using IMAP?](#MarkAsRead)

### <a name="ProtocolLog">How can I get a protocol log for IMAP, POP3, or SMTP to see what is going wrong?</a>

All of MailKit's client implementations have a constructor that takes a nifty `IProtocolLogger`
interface for logging client/server communications. Out of the box, you can use the
handy `ProtocolLogger` class. Here are some examples of how to use it:

```csharp
// log to a file called 'imap.log'
var client = new ImapClient (new ProtocolLogger ("imap.log"));
```

```csharp
// log to standard output (i.e. the console)
var client = new ImapClient (new ProtocolLogger (Console.OpenStandardOutput ()));
```

### <a name="GMailHiddenMessages">Why doesn't MailKit find some of my GMail POP3 or IMAP messages?</a>

By default, GMail's POP3 and IMAP server does not behave like standard POP3 or IMAP servers
and hides messages from clients using those protocols (as well as having other non-standard
behavior).

If you want to configure your GMail POP3 or IMAP settings to behave the way POP3 and IMAP are
intended to behave according to their protocol specifications, you'll need to log in to your
GMail account via your web browser and navigate to the `Forwarding and POP/IMAP` tab of your
GMail Settings page and set your options to look like this:

![GMail POP3 and IMAP Settings](http://content.screencast.com/users/jeff.xamarin/folders/Jing/media/7d50dada-6cb0-4ab1-b117-8600fb5e07d4/00000022.png "GMail POP3 and IMAP Settings")

### <a name="GMailOAuth2">How can I log in to a GMail account using OAuth 2.0?</a>

The first thing you need to do is follow
[Google's instructions](https://developers.google.com/accounts/docs/OAuth2) 
for obtaining OAuth 2.0 credentials for your application.

Once you've done that, the easiest way to obtain an access token is to use Google's 
[Google.Apis.Auth](https://www.nuget.org/packages/Google.Apis.Auth/) library:

```csharp
var certificate = new X509Certificate2 (@"C:\path\to\certificate.p12", "password", X509KeyStorageFlags.Exportable);
var credential = new ServiceAccountCredential (new ServiceAccountCredential
    .Initializer ("your-developer-id@developer.gserviceaccount.com") {
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

### <a name="SearchBetween2Dates">How can I search for messages delivered between two dates?</a>

The obvious solution is:

```csharp
var query = SearchQuery.DeliveredAfter (dateRange.BeginDate)
    .And (SearchQuery.DeliveredBefore (dateRange.EndDate));
var results = folder.Search (query);
```

However, it has been reported to me that this doesn't work reliably depending on the IMAP server implementation.

If you find that this query doesn't get the expected results for your IMAP server, here's another solution that
should always work:

```csharp
var query = SearchQuery.Not (SearchQuery.DeliveredBefore (dateRange.BeginDate)
    .Or (SearchQuery.DeliveredAfter (dateRange.EndDate)));
var results = folder.Search (query);
```

### <a name="ImapClientBusy">What does "The ImapClient is currently busy processing a command." mean?</a>

If you get an InvalidOperationException with the message, "The ImapClient is currently busy processing a
command.", it means that you are trying to use the `ImapClient` and/or one of its `ImapFolder`s from multiple
threads.
To avoid this situation, you'll need to lock the `SyncRoot` property of the `ImapClient` and `ImapFolder`
objects when performing operations on them.

For example:

```csharp
lock (client.SyncRoot) {
    client.NoOp ();
}
```

Note: Locking the `SyncRoot` is only necessary when using the synchronous API's. All `Async()` method variants
already do this locking for you.

### <a name="FolderNotOpenException">Why do I get InvalidOperationException: "The folder is not currently open."?</a>

If you get this exception, it's probably because you thought you had to open the destination folder that you
passed as an argument to one of the CopyTo() or MoveTo() methods. When you opened that destination folder, you
also inadvertantly closed the source folder which is why you are getting this exception.

The IMAP server can only have a single folder open at a time. Whenever you open a folder, you automatically
close the previously opened folder.

When copying or moving messages from one folder to another, you only need to have the source folder open.

### <a name="MoveDoesNotMove">ImapFolder.MoveTo() does not move the message out of the source folder. Why not?</a>

If you look at the source code for the `ImapFolder.MoveTo()` method, what you'll notice is that
there are several code paths depending on the features that the IMAP server supports.

If the IMAP server supports the `MOVE` extension, then MailKit's `MoveTo()` method will use the
`MOVE` command. I suspect that your server does not support the `MOVE` command or you probably
wouldn't be seeing what you are seeing.

When the IMAP server does not support the `MOVE` command, MailKit has to use the `COPY` command to
copy the message(s) to the destination folder. Once the `COPY` command has completed, it will then
mark the messages that you asked it to move for deletion by setting the `\Deleted` flag on those
messages.

If the server supports the `UIDPLUS` extension, then MailKit will attempt to `EXPUNGE` the subset of
messages that it just marked for deletion, however, if the `UIDPLUS` extension is not supported by the
IMAP server, then it cannot safely expunge just that subset of messages and so it simply stops there.

My guess is that your server supports neither `MOVE` nor `UIDPLUS` and that is why clients like Outlook
continue to see the messages in your folder. I believe, however, that Outlook has a setting to show
deleted messages with a strikeout (which you probably have disabled).

So to answer your question more succinctly: After calling `folder.MoveTo (...);`, if you are confident
that the messages marked for deletion should be expunged, call `folder.Expunge ();`

### <a name="MarkAsRead">How can I mark messages as read for IMAP?</a>

The way to mark messages as read using the IMAP protocol is to set the `\Seen` flag on the message(s).

To do this using MailKit, you will first need to know either the index(es) or the UID(s) of the messages
that you would like to set the `\Seen` flag on. Once you have that information, you will want to call
one of the `AddFlags()` methods on the `ImapFolder`. For example:

```csharp
folder.AddFlags (uids, MessageFlags.Seen, true);
```

To mark messages as unread, you would *remove* the `\Seen` flag, like so:

```csharp
folder.RemoveFlags (uids, MessageFlags.Seen, true);
```
