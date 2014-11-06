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
