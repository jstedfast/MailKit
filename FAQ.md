# Frequently Asked Questions

## Question Index

### General
* [Are MimeKit and MailKit completely free? Can I use them in my proprietary product(s)?](#CompletelyFree)
* [Why do I get `The remote certificate is invalid according to the validation procedure` when I try to Connect?](#InvalidSslCertificate)
* [How can I get a protocol log for IMAP, POP3, or SMTP to see what is going wrong?](#ProtocolLog)
* [How can I cancel the Connect() or ConnectAsync() methods or override the timeout?](#CancelConnect)
* [Why doesn't MailKit find some of my GMail POP3 or IMAP messages?](#GMailHiddenMessages)
* [How can I access GMail using MailKit?](#GMailAccess)
* [How can I log in to a GMail account using OAuth 2.0?](#GMailOAuth2)

### Messages
* [How can I create a message with attachments?](#CreateAttachments)
* [How can I get the main body of a message?](#MessageBody)
* [How can I tell if a message has attachments?](#HasAttachments)
* [Why doesn't the `MimeMessage` class implement `ISerializable` so that I can serialize a message to disk and read it back later?](#Serialize)
* [How can I parse messages?](#LoadMessages)
* [How can I save messages?](#SaveMessages)
* [How can I save attachments?](#SaveAttachments)
* [How can I get the email addresses in the From, To, and Cc headers?](#AddressHeaders)
* [Why do attachments with unicode filenames appear as "ATT0####.dat" in Outlook?](#UntitledAttachments)
* [How can I decrypt PGP messages that are embedded in the main message text?](#DecryptInlinePGP)
* [How can I reply to a message?](#Reply)
* [How can I forward a message?](#Forward)

### ImapClient
* [How can I get the number of unread messages in a folder?](#ImapUnreadCount)
* [How can I search for messages delivered between two dates?](#ImapSearchBetween2Dates)
* [What does "The ImapClient is currently busy processing a command." mean?](#ImapClientBusy)
* [Why do I get InvalidOperationException: "The folder is not currently open."?](#FolderNotOpenException)
* [Why doesn't ImapFolder.MoveTo() move the message out of the source folder?](#ImapMoveDoesNotMove)
* [How can I mark messages as read using IMAP?](#ImapMarkAsRead)
* [How can I re-synchronize the cache for an IMAP folder?](#ImapFolderResync)

### SmtpClient
* [How can I send email to the SpecifiedPickupDirectory?](#SpecifiedPickupDirectory)
* [How can I request a notification when the message is read by the user?](#SmtpRequestReadReceipt)
* [How can I process a read receipt notification?](#SmtpProcessReadReceipt)


## General

### <a name="CompletelyFree">Q: Are MimeKit and MailKit completely free? Can I use them in my proprietary product(s)?</a>

Yes. MimeKit and MailKit are both completely free and open source. They are both covered under the
[MIT](https://opensource.org/licenses/MIT) license.

### <a name="InvalidSslCertificate">Q: Why do I get `The remote certificate is invalid according to the validation procedure` when I try to Connect?</a>

When you get an exception with that error message, it means that the IMAP, POP3 or SMTP
server that you are connecting to is using an SSL certificate that is either expired
or untrusted by your system.

Often times, mail servers will use self-signed certificates instead of using a certificate
that has been signed by a trusted Certificate Authority. When your system is unable to
validate the mail server's certificate because it is not signed by a known and trusted
Certificate Authority, the above error will occur.

You can work around this problem by supplying a custom [RemoteCertificateValidationCallback](https://msdn.microsoft.com/en-us/library/ms145054)
and setting it on the client's [ServerCertificateValidationCallback](http://mimekit.net/docs/html/P_MailKit_MailService_ServerCertificateValidationCallback.htm)
property.

In the most simplest example, you could do something like this (although I would strongly recommend against it in
production use):

```csharp
using (var client = new SmtpClient ()) {
    client.ServerCertificateValidationCallback = (s,c,h,e) => true;

    client.Connect (hostName, port, SecureSocketOptions.Auto);

    // ...
}
```

Most likely you'll want to instead compare the certificate's [Thumbprint](https://msdn.microsoft.com/en-us/library/system.security.cryptography.x509certificates.x509certificate2.thumbprint(v=vs.110).aspx)
property to a known value that you have verified at a prior date.

You could also use this callback to prompt the user (much like you have probably seen web browsers do)
as to whether or not the certificate should be trusted.

### <a name="ProtocolLog">Q: How can I get a protocol log for IMAP, POP3, or SMTP to see what is going wrong?</a>

All of MailKit's client implementations have a constructor that takes a nifty
[IProtocolLogger](http://www.mimekit.net/docs/html/T_MailKit_IProtocolLogger.htm)
interface for logging client/server communications. Out of the box, you can use the
handy [ProtocolLogger](http://www.mimekit.net/docs/html/T_MailKit_ProtocolLogger.htm) class.
Here are some examples of how to use it:

```csharp
// log to a file called 'imap.log'
var client = new ImapClient (new ProtocolLogger ("imap.log"));
```

```csharp
// log to standard output (i.e. the console)
var client = new ImapClient (new ProtocolLogger (Console.OpenStandardOutput ()));
```

**Note:** When submitting a protocol log as part of a bug report, make sure to scrub any sensitive
information including your authentication credentials. This information will generally be the base64
encoded blob immediately following an `AUTHENTICATE` or `AUTH` command (depending on the type of server).
The only exception to this case is if you are authenticating with `NTLM` in which case I *may* need this
information, but *only if* the bug/error is in the authentication step.

### <a name="CancelConnect">Q: How can I cancel the Connect() or ConnectAsync() methods or override the timeout?</a>

One of the limitations in MailKit is that the `SmtpClient`, `Pop3Client` and `ImapClient` `Connect()`/`ConnectAsync()`
methods cannot be interrupted while the underlying socket is connecting. Cancelling the `CancellationToken` and/or
overriding the client `Timeout` property will not work.

Sadly, this is because `System.Net.Sockets.Socket`'s `Connect()` method does not respect the timeout values and there
is no `ConnectAsync()` method that takes a `CancellationToken` argument.

Luckily, each of MailKit's client implementations *does* provide `Connect()` and `ConnectAsync()` methods that take
an existing `Socket` argument that has already been connected.

To interrupt a socket connecting to a remote host using a `CancellationToken`, you could do this:

```csharp
static Task ConnectAsync (Socket socket, string host, int port, CancellationToken cancellationToken)
{
	var completion = new TaskCompletionSource<bool> ();
	
	socket.BeginConnect (host, port, result => {
		try {
			socket.EndConnect (result);
			completion.TrySetResult (true);
		} catch (Exception ex) {
			completion.TrySetException (ex);
		}
	}, null);

	cancellationToken.Register (() => {
		completion.SetException (new OperationCanceledException ());
		socket.Close ();
	});
	
	return completion.Task;
}
```

### <a name="GMailHiddenMessages">Q: Why doesn't MailKit find some of my GMail POP3 or IMAP messages?</a>

By default, GMail's POP3 and IMAP server does not behave like standard POP3 or IMAP servers
and hides messages from clients using those protocols (as well as having other non-standard
behavior).

If you want to configure your GMail POP3 or IMAP settings to behave the way POP3 and IMAP are
intended to behave according to their protocol specifications, you'll need to log in to your
GMail account via your web browser and navigate to the `Forwarding and POP/IMAP` tab of your
GMail Settings page and set your options to look like this:

![GMail POP3 and IMAP Settings](http://content.screencast.com/users/jeff.xamarin/folders/Jing/media/7d50dada-6cb0-4ab1-b117-8600fb5e07d4/00000022.png "GMail POP3 and IMAP Settings")

### <a name="GMailAccess">Q: How can I access GMail using MailKit?</a>

The first thing that you will need to do is to configure your GMail account to
[enable less secure apps](https://www.google.com/settings/security/lesssecureapps),
or you'll need to use [OAuth 2.0 authentication](#GMailOAuth2) (which is a bit more complex).

Then, assuming that your GMail account is `user@gmail.com`, you would use the following
code snippet to connect to GMail via IMAP:

```csharp
using (var client = new ImapClient ()) {
    client.ServerCertificateValidationCallback = (s,c,ch,e) => true;
    client.Connect ("imap.gmail.com", 993, SecureSocketOptions.SslOnConnect);
    client.Authenticate ("user@gmail.com", "password");
    
    // do stuff...
    
    client.Disconnect (true);
}
```

Connecting via POP3 or SMTP is identical except for the host names and ports (and, of course, you'd
use a `Pop3Client` or `SmtpClient` as appropriate).

### <a name="GMailOAuth2">Q: How can I log in to a GMail account using OAuth 2.0?</a>

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
    User = "user@gmail.com"
}.FromCertificate (certificate));

bool result = await credential.RequestAccessTokenAsync (CancellationToken.None);

// Note: result will be true if the access token was received successfully
```

Now that you have an access token (`credential.Token.AccessToken`), you can use it with MailKit by using the
token to create a new OAuth2 SASL mechanism context and then authenticating with it:

```csharp
using (var client = new ImapClient ()) {
    client.Connect ("imap.gmail.com", 993, true);

    var oauth2 = new SaslMechanismOAuth2 ("user@gmail.com", credential.Token.AccessToken);
    client.Authenticate (oauth2);
}
```

## Messages

### <a name="CreateAttachments">Q: How can I create a message with attachments?</a>

To construct a message with attachments, the first thing you'll need to do is create a `multipart/mixed`
container which you'll then want to add the message body to first. Once you've added the body, you can
then add MIME parts to it that contain the content of the files you'd like to attach, being sure to set
the `Content-Disposition` header value to attachment. You'll probably also want to set the `filename`
parameter on the `Content-Disposition` header as well as the `name` parameter on the `Content-Type`
header. The most convenient way to do this is to use the
[MimePart.FileName](http://www.mimekit.net/docs/html/P_MimeKit_MimePart_FileName.htm) property which
will set both parameters for you as well as setting the `Content-Disposition` header value to `attachment`
if it has not already been set to something else.

```csharp
var message = new MimeMessage ();
message.From.Add (new MailboxAddress ("Joey", "joey@friends.com"));
message.To.Add (new MailboxAddress ("Alice", "alice@wonderland.com"));
message.Subject = "How you doin?";

// create our message text, just like before (except don't set it as the message.Body)
var body = new TextPart ("plain") {
    Text = @"Hey Alice,

What are you up to this weekend? Monica is throwing one of her parties on
Saturday and I was hoping you could make it.

Will you be my +1?

-- Joey
"
};

// create an image attachment for the file located at path
var attachment = new MimePart ("image", "gif") {
    Content = new MimeContent (File.OpenRead (path), ContentEncoding.Default),
    ContentDisposition = new ContentDisposition (ContentDisposition.Attachment),
    ContentTransferEncoding = ContentEncoding.Base64,
    FileName = Path.GetFileName (path)
};

// now create the multipart/mixed container to hold the message text and the
// image attachment
var multipart = new Multipart ("mixed");
multipart.Add (body);
multipart.Add (attachment);

// now set the multipart/mixed as the message body
message.Body = multipart;
```

A simpler way to construct messages with attachments is to take advantage of the
[BodyBuilder](http://www.mimekit.net/docs/html/T_MimeKit_BodyBuilder.htm) class.

```csharp
var message = new MimeMessage ();
message.From.Add (new MailboxAddress ("Joey", "joey@friends.com"));
message.To.Add (new MailboxAddress ("Alice", "alice@wonderland.com"));
message.Subject = "How you doin?";

var builder = new BodyBuilder ();

// Set the plain-text version of the message text
builder.TextBody = @"Hey Alice,

What are you up to this weekend? Monica is throwing one of her parties on
Saturday and I was hoping you could make it.

Will you be my +1?

-- Joey
";

// We may also want to attach a calendar event for Monica's party...
builder.Attachments.Add (@"C:\Users\Joey\Documents\party.ics");

// Now we just need to set the message body and we're done
message.Body = builder.ToMessageBody ();
```

For more information, see [Creating Messages](http://www.mimekit.net/docs/html/Creating-Messages.htm).

### <a name="MessageBody">Q: How can I get the main body of a message?</a>

(Note: for the TL;DR version, skip to [the end](#MessageBodyTLDR))

MIME is a tree structure of parts. There are multiparts which contain other parts (even other multiparts).
There are message parts which contain messages. And finally, there are leaf-node parts which contain content.

There are a few common message structures:

1. The message contains only a `text/plain` or `text/html` part (easy, just use that).

2. The message contains a `multipart/alternative` which will typically look a bit like this:

    ```
    multipart/alternative
       text/plain
       text/html
    ```

3. Same as above, but the html part is inside a `multipart/related` so that it can embed images:

    ```
    multipart/alternative
       text/plain
       multipart/related
          text/html
          image/jpeg
          image/png
    ```

4. The message contains a textual body part as well as some attachments:

    ```
    multipart/mixed
       text/plain or text/html
       application/octet-stream
       application/zip
    ```

5. the same as above, but with the first part replaced with either #2 or #3. To illustrate:

    ```
    multipart/mixed
       multipart/alternative
          text/plain
          text/html
       application/octet-stream
       application/zip
    ```

    or...

    ```
    multipart/mixed
       multipart/alternative
          text/plain
          multipart/related
             text/html
             image/jpeg
             image/png
       application/octet-stream
       application/zip
    ```

<a name="MessageBodyTLDR"></a>Now, if you don't care about any of that and just want to get the text of
the first `text/plain` or `text/html` part you can find, that's easy.

[MimeMessage](http://www.mimekit.net/docs/html/T_MimeKit_MimeMessage.htm) has two convenience properties
for this: [TextBody](http://www.mimekit.net/docs/html/P_MimeKit_MimeMessage_TextBody.htm) and
[HtmlBody](http://www.mimekit.net/docs/html/P_MimeKit_MimeMessage_HtmlBody.htm).

`MimeMessage.HtmlBody`, as the name implies, will traverse the MIME structure for you and find the most
appropriate body part with a `Content-Type` of `text/html` that can be interpreted as the message body.
Likewise, the `TextBody` property can be used to get the `text/plain` version of the message body.

For more information, see [Working with Messages](http://www.mimekit.net/docs/html/Working-With-Messages.htm).

### <a name="HasAttachments">Q: How can I tell if a message has attachments?</a>

In most cases, a message with a body that has a MIME-type of `multipart/mixed` containing more than a
single part probably has attachments. As illustrated above, the first part of a `multipart/mixed` is
typically the textual body of the message, but it is not always quite that simple.

In general, MIME attachments will have a `Content-Disposition` header with a value of `attachment`.
To get the list of body parts matching this criteria, you can use the
[MimeMessage.Attachments](http://www.mimekit.net/docs/html/P_MimeKit_MimeMessage_Attachments.htm) property.

Unfortunately, not all mail clients follow this convention and so you may need to write your own custom logic.
For example, you may wish to treat all body parts having a `name` or `filename` parameter set on them:

```csharp
var attachments = message.BodyParts.OfType<MimePart> ().Where (part => !string.IsNullOrEmpty (part.FileName));
```

A more sophisticated approach is to treat body parts not referenced by the main textual body part of the
message as attachments. In other words, treat any body part not used for rendering the message as an
attachment. For an example on how to do this, consider the following code snippets:

```csharp
/// <summary>
/// Visits a MimeMessage and generates HTML suitable to be rendered by a browser control.
/// </summary>
class HtmlPreviewVisitor : MimeVisitor
{
    List<MultipartRelated> stack = new List<MultipartRelated> ();
    List<MimeEntity> attachments = new List<MimeEntity> ();
    readonly string tempDir;
    string body;

    /// <summary>
    /// Creates a new HtmlPreviewVisitor.
    /// </summary>
    /// <param name="tempDirectory">A temporary directory used for storing image files.</param>
    public HtmlPreviewVisitor (string tempDirectory)
    {
        tempDir = tempDirectory;
    }

    /// <summary>
    /// The list of attachments that were in the MimeMessage.
    /// </summary>
    public IList<MimeEntity> Attachments {
        get { return attachments; }
    }

    /// <summary>
    /// The HTML string that can be set on the BrowserControl.
    /// </summary>
    public string HtmlBody {
        get { return body ?? string.Empty; }
    }

    protected override void VisitMultipartAlternative (MultipartAlternative alternative)
    {
        // walk the multipart/alternative children backwards from greatest level of faithfulness to the least faithful
        for (int i = alternative.Count - 1; i >= 0 && body == null; i--)
            alternative[i].Accept (this);
    }

    protected override void VisitMultipartRelated (MultipartRelated related)
    {
        var root = related.Root;

        // push this multipart/related onto our stack
        stack.Add (related);

        // visit the root document
        root.Accept (this);

        // pop this multipart/related off our stack
        stack.RemoveAt (stack.Count - 1);
    }

    // look up the image based on the img src url within our multipart/related stack
    bool TryGetImage (string url, out MimePart image)
    {
        UriKind kind;
        int index;
        Uri uri;

        if (Uri.IsWellFormedUriString (url, UriKind.Absolute))
            kind = UriKind.Absolute;
        else if (Uri.IsWellFormedUriString (url, UriKind.Relative))
            kind = UriKind.Relative;
        else
            kind = UriKind.RelativeOrAbsolute;

        try {
            uri = new Uri (url, kind);
        } catch {
            image = null;
            return false;
        }

        for (int i = stack.Count - 1; i >= 0; i--) {
            if ((index = stack[i].IndexOf (uri)) == -1)
                continue;

            image = stack[i][index] as MimePart;
            return image != null;
        }

        image = null;

        return false;
    }

    // Save the image to our temp directory and return a "file://" url suitable for
    // the browser control to load.
    // Note: if you'd rather embed the image data into the HTML, you can construct a
    // "data:" url instead.
    string SaveImage (MimePart image, string url)
    {
        string fileName = url.Replace (':', '_').Replace ('\\', '_').Replace ('/', '_');

        string path = Path.Combine (tempDir, fileName);

        if (!File.Exists (path)) {
            using (var output = File.Create (path))
                image.Content.DecodeTo (output);
        }

        return "file://" + path.Replace ('\\', '/');
    }

    // Replaces <img src=...> urls that refer to images embedded within the message with
    // "file://" urls that the browser control will actually be able to load.
    void HtmlTagCallback (HtmlTagContext ctx, HtmlWriter htmlWriter)
    {
        if (ctx.TagId == HtmlTagId.Image && !ctx.IsEndTag && stack.Count > 0) {
            ctx.WriteTag (htmlWriter, false);

            // replace the src attribute with a file:// URL
            foreach (var attribute in ctx.Attributes) {
                if (attribute.Id == HtmlAttributeId.Src) {
                    MimePart image;
                    string url;

                    if (!TryGetImage (attribute.Value, out image)) {
                        htmlWriter.WriteAttribute (attribute);
                        continue;
                    }

                    url = SaveImage (image, attribute.Value);

                    htmlWriter.WriteAttributeName (attribute.Name);
                    htmlWriter.WriteAttributeValue (url);
                } else {
                    htmlWriter.WriteAttribute (attribute);
                }
            }
        } else if (ctx.TagId == HtmlTagId.Body && !ctx.IsEndTag) {
            ctx.WriteTag (htmlWriter, false);

            // add and/or replace oncontextmenu="return false;"
            foreach (var attribute in ctx.Attributes) {
                if (attribute.Name.ToLowerInvariant () == "oncontextmenu")
                    continue;

                htmlWriter.WriteAttribute (attribute);
            }

            htmlWriter.WriteAttribute ("oncontextmenu", "return false;");
        } else {
            // pass the tag through to the output
            ctx.WriteTag (htmlWriter, true);
        }
    }

    protected override void VisitTextPart (TextPart entity)
    {
        TextConverter converter;

        if (body != null) {
            // since we've already found the body, treat this as an attachment
            attachments.Add (entity);
            return;
        }

        if (entity.IsHtml) {
            converter = new HtmlToHtml {
                HtmlTagCallback = HtmlTagCallback
            };
        } else if (entity.IsFlowed) {
            var flowed = new FlowedToHtml ();
            string delsp;

            if (entity.ContentType.Parameters.TryGetValue ("delsp", out delsp))
                flowed.DeleteSpace = delsp.ToLowerInvariant () == "yes";

            converter = flowed;
        } else {
            converter = new TextToHtml ();
        }

        body = converter.Convert (entity.Text);
    }

    protected override void VisitTnefPart (TnefPart entity)
    {
        // extract any attachments in the MS-TNEF part
        attachments.AddRange (entity.ExtractAttachments ());
    }

    protected override void VisitMessagePart (MessagePart entity)
    {
        // treat message/rfc822 parts as attachments
        attachments.Add (entity);
    }

    protected override void VisitMimePart (MimePart entity)
    {
        // realistically, if we've gotten this far, then we can treat this as an attachment
        // even if the IsAttachment property is false.
        attachments.Add (entity);
    }
}
```

And the way you'd use this visitor might look something like this:

```csharp
void Render (MimeMessage message)
{
    var tmpDir = Path.Combine (Path.GetTempPath (), message.MessageId);
    var visitor = new HtmlPreviewVisitor (tmpDir);

    Directory.CreateDirectory (tmpDir);

    message.Accept (visitor);

    DisplayHtml (visitor.HtmlBody);
    DisplayAttachments (visitor.Attachments);
}
```

Once you've rendered the message using the above technique, you'll have a list of attachments that
were not used, even if they did not match the simplistic criteria used by the `MimeMessage.Attachments`
property.

### <a name="Serialize">Q: Why doesn't the `MimeMessage` class implement `ISerializable` so that I can serialize a message to disk and read it back later?</a>

The MimeKit API was designed to use the existing MIME format for serialization. In light of this, the ability
to use the .NET serialization API and format did not make much sense to support.

You can easily serialize a [MimeMessage](http://www.mimekit.net/docs/html/T_MimeKit_MimeMessage.htm) to a stream using the
[WriteTo](http://www.mimekit.net/docs/html/Overload_MimeKit_MimeMessage_WriteTo.htm) methods.

For more information on this topic, see the following other two topics:

* <a href="#LoadMessages">How can I parse messages?</a>
* <a href="#SaveMessages">How can I save messages?</a>

### <a name="LoadMessages">Q: How can I parse messages?</a>

One of the more common operations that MimeKit is meant for is parsing email messages from arbitrary streams.
There are two ways of accomplishing this task.

The first way is to use one of the [Load](http://www.mimekit.net/docs/html/Overload_MimeKit_MimeMessage_Load.htm) methods
on `MimeMessage`:

```csharp
// Load a MimeMessage from a stream
var message = MimeMessage.Load (stream);
```

Or you can load a message from a file path:

```csharp
// Load a MimeMessage from a file path
var message = MimeMessage.Load ("message.eml");
```

The second way is to use the [MimeParser](http://www.mimekit.net/docs/html/T_MimeKit_MimeParser.htm) class. For the most
part, using the `MimeParser` directly is not necessary unless you wish to parse a Unix mbox file stream. However, this is
how you would do it:

```csharp
// Load a MimeMessage from a stream
var parser = new MimeParser (stream, MimeFormat.Entity);
var message = parser.ParseMessage ();
```

For Unix mbox file streams, you would use the parser like this:

```csharp
// Load every message from a Unix mbox
var parser = new MimeParser (stream, MimeFormat.Mbox);
while (!parser.IsEndOfStream) {
    var message = parser.ParseMessage ();

    // do something with the message
}
```

### <a name="SaveMessages">Q: How can I save messages?</a>

One you've got a [MimeMessage](http://www.mimekit.net/docs/html/T_MimeKit_MimeMessage.htm), you can save
it to a file using the [WriteTo](http://mimekit.net/docs/html/Overload_MimeKit_MimeMessage_WriteTo.htm) method:

```csharp
message.WriteTo ("message.eml");
```

The `WriteTo` method also has overloads that allow you to write the message to a `Stream` instead.

By default, the `WriteTo` method will save the message using DOS line-endings on Windows and Unix
line-endings on Unix-based systems such as macOS and Linux. You can override this behavior by
passing a [FormatOptions](http://mimekit.net/docs/html/T_MimeKit_FormatOptions.htm) argument to
the method:

```csharp
// clone the default formatting options
var format = FormatOptions.Default.Clone ();

// override the line-endings to be DOS no matter what platform we are on
format.NewLineFormat = NewLineFormat.Dos;

message.WriteTo (format, "message.eml");
```

Note: While it may seem like you can safely use the `ToString` method to serialize a message,
***DON'T DO IT!*** This is ***not*** safe! MIME messages cannot be accurately represented as
strings due to the fact that each MIME part of the message *may* be encoded in a different
character set, thus making it impossible to convert the message into a unicode string using a
single charset to do the conversion (which is *exactly* what `ToString` does).

### <a name="SaveAttachments">Q: How can I save attachments?</a>

If you've already got a [MimePart](http://www.mimekit.net/docs/html/T_MimeKit_MimePart.htm) that represents
the attachment that you'd like to save, here's how you might save it:

```csharp
using (var stream = File.Create (fileName))
    attachment.Content.DecodeTo (stream);
```

Pretty simple, right?

But what if your attachment is actually a [MessagePart](http://www.mimekit.net/docs/html/T_MimeKit_MessagePart.htm)?

To save the content of a `message/rfc822` part, you'd use the following code snippet:

```csharp
using (var stream = File.Create (fileName))
    attachment.Message.WriteTo (stream);
```

If you are iterating over all of the attachments in a message, you might do something like this:

```csharp
foreach (var attachment in message.Attachments) {
    var fileName = attachment.ContentDisposition?.FileName ?? attachment.ContentType.Name;
    
    using (var stream = File.Create (fileName)) {
        if (attachment is MessagePart) {
            var rfc822 = (MessagePart) attachment;
            
            rfc822.Message.WriteTo (stream);
        } else {
            var part = (MimePart) attachment;
            
            part.Content.DecodeTo (stream);
        }
    }
}
```

### <a name="AddressHeaders">Q: How can I get the email addresses in the From, To, and Cc headers?</a>

The [From](http://www.mimekit.net/docs/html/P_MimeKit_MimeMessage_From.htm), 
[To](http://www.mimekit.net/docs/html/P_MimeKit_MimeMessage_To.htm), and 
[Cc](http://www.mimekit.net/docs/html/P_MimeKit_MimeMessage_Cc.htm) properties of a
[MimeMessage](http://www.mimekit.net/docs/html/T_MimeKit_MimeMessage.htm) are all of type
[InternetAddressList](http://www.mimekit.net/docs/html/T_MimeKit_InternetAddressList.htm). An
`InternetAddressList` is a list of
[InternetAddress](http://www.mimekit.net/docs/html/T_MimeKit_InternetAddress.htm) items. This is
where most people start to get lost because an `InternetAddress` is an abstract class that only
really has a [Name](http://www.mimekit.net/docs/html/P_MimeKit_InternetAddress_Name.htm) property.

As you've probably already discovered, the `Name` property contains the name of the person
(if available), but what you want is his or her email address, not their name.

To get the email address, you'll need to figure out what subclass of address each `InternetAddress`
really is. There are 2 subclasses of `InternetAddress`:
[GroupAddress](http://www.mimekit.net/docs/html/T_MimeKit_GroupAddress.htm) and
[MailboxAddress](http://www.mimekit.net/docs/html/T_MimeKit_MailboxAddress.htm).

A `GroupAddress` is a named group of more `InternetAddress` items that are contained within the
[Members](http://www.mimekit.net/docs/html/P_MimeKit_GroupAddress_Members.htm) property. To get
an idea of what a group address represents, consider the following examples:

```
To: My Friends: Joey <joey@friends.com>, Monica <monica@friends.com>, "Mrs. Chanandler Bong"
    <chandler@friends.com>, Ross <ross@friends.com>, Rachel <rachel@friends.com>;
```

In the above example, the `To` header's `InternetAddressList` will contain only 1 item which will be a
`GroupAddress` with a `Name` value of `My Friends`. The `Members` property of the `GroupAddress` will
contain 5 more `InternetAddress` items (which will all be instances of `MailboxAddress`).

The above example, however, is not very likely to ever be seen in messages you deal with. A far more
common example would be the one below:

```
To: undisclosed-recipients:;
```

Most of the time, the `From`, `To`, and `Cc` headers will only contain mailbox addresses. As you will
notice, a `MailboxAddress` has an
[Address](http://www.mimekit.net/docs/html/P_MimeKit_MailboxAddress_Address.htm) property which will
contain the email address of the mailbox. In the following example, the `Address` property will
contain the value `john@smith.com`:

```
To: John Smith <john@smith.com>
```

If you only care about getting a flattened list of the mailbox addresses in a `From`, `To`, or `Cc`
header, you can do something like this:

```csharp
foreach (var mailbox in message.To.Mailboxes)
    Console.WriteLine ("{0}'s email address is {1}", mailbox.Name, mailbox.Address);
```

### <a name="UntitledAttachments">Q: Why do attachments with unicode filenames appear as "ATT0####.dat" in Outlook?</a>

An attachment filename is stored as a MIME parameter on the `Content-Disposition` header. Unfortunately,
the original MIME specifications did not specify a method for encoding non-ASCII filenames. In 1997,
[rfc2184](https://tools.ietf.org/html/rfc2184) (later updated by [rfc2231](https://tools.ietf.org/html/rfc2231))
was published which specified an encoding mechanism to use for encoding them. Since there was a window in
time where the MIME specifications did not define a way to encode them, some mail client developers decided
to use the mechanism described by [rfc2047](https://tools.ietf.org/html/rfc2047) which was meant for
encoding non-ASCII text in headers. While this may at first seem logical, the problem with this approach
was that rfc2047 `encoded-word` tokens are not allowed to be in quotes (as well as some other issues) and
so another, more appropriate, encoding mechanism was needed.

Outlook is one of those mail clients which decided to encode filenames using the mechanism described in
rfc2047 and until Outlook 2007, did not support filenames encoded using the mechanism defined in rfc2231.

As of MimeKit v1.2.18, it is possible to configure MimeKit to use the rfc2047 encoding mechanism for
filenames in the following two ways:

The first way is to set the encoding method on each individual
[Parameter](http://www.mimekit.net/docs/html/T_MimeKit_Parameter.htm):

```csharp
Parameter param;

if (attachment.ContentDisposition.Parameters.TryGetValue ("filename", out param))
    param.EncodingMethod = ParameterEncodingMethod.Rfc2047;
```

The other way is to use a [FormatOptions](http://www.mimekit.net/docs/html/T_MimeKit_FormatOptions.htm):

```csharp
var options = FormatOptions.Default.Clone ();
options.ParameterEncodingMethod = ParameterEncodingMethod.Rfc2047;

message.WriteTo (options, stream);
```

### <a name="DecryptInlinePGP">Q: How can I decrypt PGP messages that are embedded in the main message text?</a>

Some PGP-enabled mail clients, such as Thunderbird, embed encrypted PGP blurbs within the `text/plain` body
of the message rather than using the PGP/MIME format that MimeKit prefers.

These messages often look something like this:

    Return-Path: <pgp-enthusiast@example.com>
    Received: from [127.0.0.1] (hostname.example.com. [201.95.8.17])
        by mx.google.com with ESMTPSA id l67sm26628445yha.8.2014.04.27.13.49.44
        for <pgp-enthusiast@example.com>
        (version=TLSv1 cipher=ECDHE-RSA-RC4-SHA bits=128/128);
        Sun, 27 Apr 2014 13:49:44 -0700 (PDT)
    Message-ID: <535D6D67.8020803@example.com>
    Date: Sun, 27 Apr 2014 17:49:43 -0300
    From: Die-Hard PGP Fan <pgp-enthusiast@example.com>
    User-Agent: Mozilla/5.0 (Windows NT 6.3; WOW64; rv:24.0) Gecko/20100101 Thunderbird/24.4.0
    MIME-Version: 1.0
    To: undisclosed-recipients:;
    Subject: Test of inline encrypted PGP blocks
    X-Enigmail-Version: 1.6
    Content-Type: text/plain; charset=ISO-8859-1
    Content-Transfer-Encoding: 8bit
    X-Antivirus: avast! (VPS 140427-1, 27/04/2014), Outbound message
    X-Antivirus-Status: Clean
    
    -----BEGIN PGP MESSAGE-----
    Charset: ISO-8859-1
    Version: GnuPG v2.0.22 (MingW32)
    Comment: Using GnuPG with Thunderbird - http://www.enigmail.net/
    
    SGFoISBJIGZvb2xlZCB5b3UsIHRoaXMgdGV4dCBpc24ndCBhY3R1YWxseSBlbmNy
    eXB0ZWQgd2l0aCBQR1AsCml0J3MgYWN0dWFsbHkgb25seSBiYXNlNjQgZW5jb2Rl
    ZCEKCkknbSBqdXN0IHVzaW5nIHRoaXMgYXMgYW4gZXhhbXBsZSwgdGhvdWdoLCBz
    byBpdCBkb2Vzbid0IHJlYWxseSBtYXR0ZXIuCgpGb3IgdGhlIHNha2Ugb2YgYXJn
    dW1lbnQsIHdlJ2xsIHByZXRlbmQgdGhhdCB0aGlzIGlzIGFjdHVhbGx5IGFuIGVu
    Y3J5cHRlZApibHVyYi4gTW1ta2F5PyBUaGFua3MuCg==
    -----END PGP MESSAGE-----

To deal with these kinds of messages, I've added a method to OpenPgpContext called `GetDecryptedStream` which
can be used to get the raw decrypted stream.

There are actually 2 variants of this method:

```csharp
public Stream GetDecryptedStream (Stream encryptedData, out DigitalSignatureCollection signatures)
```

and

```csharp
public Stream GetDecryptedStream (Stream encryptedData)
```

The first variant is useful in cases where the encrypted PGP blurb is also digitally signed, allowing you to get
your hands on the list of digitial signatures in order for you to verify each of them.

To decrypt the content of the message, you'll want to locate the `TextPart` (in this case, it'll just be 
`message.Body`)
and then do this:

```
static Stream DecryptEmbeddedPgp (TextPart text)
{
    using (var memory = new MemoryStream ()) {
        text.Content.DecodeTo (memory);
        memory.Position = 0;

        using (var ctx = new MyGnuPGContext ()) {
            return ctx.GetDecryptedStream (memory);
        }
    }
}
```

What you do with that decrypted stream is up to you. It's up to you to figure out what the decrypted content is
(is it text? a jpeg image? a video?) and how to display it to the user.

### <a name="Reply">Q: How can I reply to a message?</a>

Replying to a message is fairly simple. For the most part, you'd just create the reply message
the same way you'd create any other message. There are only a few slight differences:

1. In the reply message, you'll want to prefix the `Subject` header with `"Re: "` if the prefix
   doesn't already exist in the message you are replying to (in other words, if you are replying
   to a message with a `Subject` of `"Re: party tomorrow night!"`, you would not prefix it with
   another `"Re: "`).
2. You will want to set the reply message's `In-Reply-To` header to the value of the
   `Message-Id` header in the original message.
3. You will want to copy the original message's `References` header into the reply message's
   `References` header and then append the original message's `Message-Id` header.
4. You will probably want to "quote" the original message's text in the reply.

If this logic were to be expressed in code, it might look something like this:

```csharp
public static MimeMessage Reply (MimeMessage message, MailboxAddress from, bool replyToAll)
{
	var reply = new MimeMessage ();

	reply.From.Add (from);

	// reply to the sender of the message
	if (message.ReplyTo.Count > 0) {
		reply.To.AddRange (message.ReplyTo);
	} else if (message.From.Count > 0) {
		reply.To.AddRange (message.From);
	} else if (message.Sender != null) {
		reply.To.Add (message.Sender);
	}

	if (replyToAll) {
		// include all of the other original recipients - TODO: remove ourselves from these lists
		reply.To.AddRange (message.To);
		reply.Cc.AddRange (message.Cc);
	}

	// set the reply subject
	if (!message.Subject.StartsWith ("Re:", StringComparison.OrdinalIgnoreCase))
		reply.Subject = "Re: " + message.Subject;
	else
		reply.Subject = message.Subject;

	// construct the In-Reply-To and References headers
	if (!string.IsNullOrEmpty (message.MessageId)) {
		reply.InReplyTo = message.MessageId;
		foreach (var id in message.References)
			reply.References.Add (id);
		reply.References.Add (message.MessageId);
	}

	// quote the original message text
	using (var quoted = new StringWriter ()) {
		var sender = message.Sender ?? message.From.Mailboxes.FirstOrDefault ();

		quoted.WriteLine ("On {0}, {1} wrote:", message.Date.ToString ("f"), !string.IsNullOrEmpty (sender.Name) ? sender.Name : sender.Address);
		using (var reader = new StringReader (message.TextBody)) {
			string line;

			while ((line = reader.ReadLine ()) != null) {
				quoted.Write ("> ");
				quoted.WriteLine (line);
			}
		}

		reply.Body = new TextPart ("plain") {
			Text = quoted.ToString ()
		};
	}

	return reply;
}
```

But what if you wanted to reply to a message and quote the HTML formatting of the original message
body (assuming it has an HTML body) while still including the embedded images?

This gets a bit more complicated, but it's still doable...

The first thing we'd need to do is implement our own
[MimeVisitor](http://www.mimekit.net/docs/html/T_MimeKit_MimeVisitor.htm) to handle this:

```csharp
public class ReplyVisitor : MimeVisitor
{
	readonly Stack<Multipart> stack = new Stack<Multipart> ();
	MimeMessage original, reply;
	MailboxAddress from;
	bool replyToAll;

	/// <summary>
	/// Creates a new ReplyVisitor.
	/// </summary>
	public ReplyVisitor (MailboxAddress from, bool replyToAll)
	{
		this.replyToAll = replyToAll;
		this.from = from;
	}

	/// <summary>
	/// Gets the reply.
	/// </summary>
	/// <value>The reply.</value>
	public MimeMessage Reply {
		get { return reply; }
	}

	void Push (MimeEntity entity)
	{
		var multipart = entity as Multipart;

		if (reply.Body == null) {
			reply.Body = entity;
		} else {
			var parent = stack.Peek ();
			parent.Add (entity);
		}

		if (multipart != null)
			stack.Push (multipart);
	}

	void Pop ()
	{
		stack.Pop ();
	}

	static string GetOnDateSenderWrote (MimeMessage message)
	{
		var sender = message.Sender != null ? message.Sender : message.From.Mailboxes.FirstOrDefault ();
		var name = sender != null ? (!string.IsNullOrEmpty (sender.Name) ? sender.Name : sender.Address) : "an unknown sender";

		return string.Format ("On {0}, {1} wrote:", message.Date.ToString ("f"), name);
	}

	/// <summary>
	/// Visit the specified message.
	/// </summary>
	/// <param name="message">The message.</param>
	public override void Visit (MimeMessage message)
	{
		reply = new MimeMessage ();
		original = message;

		stack.Clear ();

		reply.From.Add (from.Clone ());

		// reply to the sender of the message
		if (message.ReplyTo.Count > 0) {
			reply.To.AddRange (message.ReplyTo);
		} else if (message.From.Count > 0) {
			reply.To.AddRange (message.From);
		} else if (message.Sender != null) {
			reply.To.Add (message.Sender);
		}

		if (replyToAll) {
			// include all of the other original recipients - TODO: remove ourselves from these lists
			reply.To.AddRange (message.To);
			reply.Cc.AddRange (message.Cc);
		}

		// set the reply subject
		if (!message.Subject.StartsWith ("Re:", StringComparison.OrdinalIgnoreCase))
			reply.Subject = "Re: " + message.Subject;
		else
			reply.Subject = message.Subject;

		// construct the In-Reply-To and References headers
		if (!string.IsNullOrEmpty (message.MessageId)) {
			reply.InReplyTo = message.MessageId;
			foreach (var id in message.References)
				reply.References.Add (id);
			reply.References.Add (message.MessageId);
		}

		base.Visit (message);
	}

	/// <summary>
	/// Visit the specified entity.
	/// </summary>
	/// <param name="entity">The MIME entity.</param>
	/// <exception cref="System.NotSupportedException">
	/// Only Visit(MimeMessage) is supported.
	/// </exception>
	public override void Visit (MimeEntity entity)
	{
		throw new NotSupportedException ();
	}

	protected override void VisitMultipartAlternative (MultipartAlternative alternative)
	{
		var multipart = new MultipartAlternative ();

		Push (multipart);

		for (int i = 0; i < alternative.Count; i++)
			alternative[i].Accept (this);

		Pop ();
	}

	protected override void VisitMultipartRelated (MultipartRelated related)
	{
		var multipart = new MultipartRelated ();
		var root = related.Root;

		Push (multipart);

		root.Accept (this);

		for (int i = 0; i < related.Count; i++) {
			if (related[i] != root)
				related[i].Accept (this);
		}

		Pop ();
	}

	protected override void VisitMultipart (Multipart multipart)
	{
		foreach (var part in multipart) {
			if (part is MultipartAlternative)
				part.Accept (this);
			else if (part is MultipartRelated)
				part.Accept (this);
			else if (part is TextPart)
				part.Accept (this);
		}
	}

	void HtmlTagCallback (HtmlTagContext ctx, HtmlWriter htmlWriter)
	{
		if (ctx.TagId == HtmlTagId.Body && !ctx.IsEmptyElementTag) {
			if (ctx.IsEndTag) {
				// end our opening <blockquote>
				htmlWriter.WriteEndTag (HtmlTagId.BlockQuote);

				// pass the </body> tag through to the output
				ctx.WriteTag (htmlWriter, true);
			} else {
				// pass the <body> tag through to the output
				ctx.WriteTag (htmlWriter, true);

				// prepend the HTML reply with "On {DATE}, {SENDER} wrote:"
				htmlWriter.WriteStartTag (HtmlTagId.P);
				htmlWriter.WriteText (GetOnDateSenderWrote (original));
				htmlWriter.WriteEndTag (HtmlTagId.P);

				// Wrap the original content in a <blockquote>
				htmlWriter.WriteStartTag (HtmlTagId.BlockQuote);
				htmlWriter.WriteAttribute (HtmlAttributeId.Style, "border-left: 1px #ccc solid; margin: 0 0 0 .8ex; padding-left: 1ex;");

				ctx.InvokeCallbackForEndTag = true;
			}
		} else {
			// pass the tag through to the output
			ctx.WriteTag (htmlWriter, true);
		}
	}

	string QuoteText (string text)
	{
		using (var quoted = new StringWriter ()) {
			quoted.WriteLine (GetOnDateSenderWrote (original));

			using (var reader = new StringReader (text)) {
				string line;

				while ((line = reader.ReadLine ()) != null) {
					quoted.Write ("> ");
					quoted.WriteLine (line);
				}
			}

			return quoted.ToString ();
		}
	}

	protected override void VisitTextPart (TextPart entity)
	{
		string text;

		if (entity.IsHtml) {
			var converter = new HtmlToHtml {
				HtmlTagCallback = HtmlTagCallback
			};

			text = converter.Convert (entity.Text);
		} else if (entity.IsFlowed) {
			var converter = new FlowedToText ();

			text = converter.Convert (entity.Text);
			text = QuoteText (text);
		} else {
			// quote the original message text
			text = QuoteText (entity.Text);
		}

		var part = new TextPart (entity.ContentType.MediaSubtype.ToLowerInvariant ()) {
			Text = text
		};

		Push (part);
	}

	protected override void VisitMessagePart (MessagePart entity)
	{
		// don't descend into message/rfc822 parts
	}
}
```

```csharp
public static MimeMessage Reply (MimeMessage message, MailboxAddress from, bool replyToAll)
{
	var visitor = new ReplyVisitor (from, replyToAll);

	visitor.Visit (message);

	return visitor.Reply;
}
```

### <a name="Forward">Q: How can I forward a message?</a>

There are 2 common ways of forwarding a message: attaching the original message as an attachment and inlining
the message body much like replying typically does. Which method you choose is up to you.

To forward a message by attaching it as an attachment, you would do do something like this:

```csharp
public static MimeMessage Forward (MimeMessage original, MailboxAddress from, IEnumerable<InternetAddress> to)
{
	var message = new MimeMessage ();
	message.From.Add (from);
	message.To.AddRange (to);

	// set the forwarded subject
	if (!original.Subject.StartsWith ("FW:", StringComparison.OrdinalIgnoreCase))
		message.Subject = "FW: " + original.Subject;
	else
		message.Subject = original.Subject;

	// create the main textual body of the message
	var text = new TextPart ("plain") { Text = "Here's the forwarded message:" };

	// create the message/rfc822 attachment for the original message
	var rfc822 = new MessagePart { Message = original };
    
	// create a multipart/mixed container for the text body and the forwarded message
	var multipart = new Multipart ("mixed");
	multipart.Add (text);
	multipart.Add (rfc822);

	// set the multipart as the body of the message
	message.Body = multipart;

	return message;
}
```

To forward a message by inlining the original message's text content, you can do something like this:

```csharp
public static MimeMessage Forward (MimeMessage original, MailboxAddress from, IEnumerable<InternetAddress> to)
{
	var message = new MimeMessage ();
	message.From.Add (from);
	message.To.AddRange (to);

	// set the forwarded subject
	if (!original.Subject.StartsWith ("FW:", StringComparison.OrdinalIgnoreCase))
		message.Subject = "FW: " + original.Subject;
	else
		message.Subject = original.Subject;

	// quote the original message text
	using (var text = new StringWriter ()) {
		text.WriteLine ();
		text.WriteLine ("-------- Original Message --------");
		text.WriteLine ("Subject: {0}", original.Subject);
		text.WriteLine ("Date: {0}", DateUtils.FormatDate (original.Date));
		text.WriteLine ("From: {0}", original.From);
		text.WriteLine ("To: {0}", original.To);
		text.WriteLine ();
		
		text.Write (original.TextBody);

		message.Body = new TextPart ("plain") {
			Text = text.ToString ()
		};
	}

	return message;
}
```

Keep in mind that not all messages will have a `TextBody` available, so you'll have to find a way to handle those cases.

## ImapClient

### <a name="ImapUnreadCount">Q: How can I get the number of unread messages in a folder?</a>

If the folder is open (via [Open](http://www.mimekit.net/docs/html/Overload_MailKit_Net_Imap_ImapFolder_Open.htm)),
then the [ImapFolder.Unread](http://www.mimekit.net/docs/html/P_MailKit_MailFolder_Unread.htm) property will be kept
up to date (at least as-of the latest command issued to the server).

If the folder *isn't* open, then you will need to query the unread state of the folder using the
[Status](http://www.mimekit.net/docs/html/M_MailKit_Net_Imap_ImapFolder_Status.htm) method with the
appropriate [StatusItems](http://www.mimekit.net/docs/html/T_MailKit_StatusItems.htm) flag(s).

For example, to get the total *and* unread counts, you can do this:

```csharp
folder.Status (StatusItems.Count | StatusItems.Unread);

int total = folder.Count;
int unread = folder.Unread;
```

### <a name="ImapSearchBetween2Dates">Q: How can I search for messages delivered between two dates?</a>

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

### <a name="ImapClientBusy">Q: What does "The ImapClient is currently busy processing a command." mean?</a>

If you get an InvalidOperationException with the message, "The ImapClient is currently busy processing a
command.", it means that you are trying to use the
[ImapClient](http://www.mimekit.net/docs/html/T_MailKit_Net_Imap_ImapClient.htm) and/or one of its
[ImapFolder](http://www.mimekit.net/docs/html/T_MailKit_Net_Imap_ImapFolder.htm)s from multiple
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

### <a name="FolderNotOpenException">Q: Why do I get InvalidOperationException: "The folder is not currently open."?</a>

If you get this exception, it's probably because you thought you had to open the destination folder that you
passed as an argument to one of the
[CopyTo](http://www.mimekit.net/docs/html/Overload_MailKit_MailFolder_CopyTo.htm) or
[MoveTo](http://www.mimekit.net/docs/html/Overload_MailKit_MailFolder_MoveTo.htm) methods. When you opened
that destination folder, you also inadvertantly closed the source folder which is why you are getting this
exception.

The IMAP server can only have a single folder open at a time. Whenever you open a folder, you automatically
close the previously opened folder.

When copying or moving messages from one folder to another, you only need to have the source folder open.

### <a name="ImapMoveDoesNotMove">Q: Why doesn't ImapFolder.MoveTo() move the message out of the source folder?</a>

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
IMAP server, then it cannot safely expunge just that subset of messages and so it stops there.

My guess is that your server supports neither `MOVE` nor `UIDPLUS` and that is why clients like Outlook
continue to see the messages in your folder. I believe, however, that Outlook has a setting to show
deleted messages with a strikeout (which you probably have disabled).

So to answer your question more succinctly: After calling `folder.MoveTo (...);`, if you are confident
that the messages marked for deletion should be expunged, call `folder.Expunge ();`

### <a name="ImapMarkAsRead">Q: How can I mark messages as read for IMAP?</a>

The way to mark messages as read using the IMAP protocol is to set the `\Seen` flag on the message(s).

To do this using MailKit, you will first need to know either the index(es) or the UID(s) of the messages
that you would like to set the `\Seen` flag on. Once you have that information, you will want to call
one of the
[AddFlags](http://www.mimekit.net/docs/html/Overload_MailKit_MailFolder_AddFlags.htm) methods on the
`ImapFolder`. For example:

```csharp
folder.AddFlags (uids, MessageFlags.Seen, true);
```

To mark messages as unread, you would *remove* the `\Seen` flag, like so:

```csharp
folder.RemoveFlags (uids, MessageFlags.Seen, true);
```

### <a name="ImapFolderResync">Q: How can I re-synchronize the cache for an IMAP folder?</a>

Assuming your IMAP server does not support the `QRESYNC` extension (which simplifies this proceedure a ton),
here is some simple code to illustrate how to go about re-synchronizing your cache with the remote IMAP
server.

```csharp
/// <summary>
/// Just a simple class to represent the cached information about a message.
/// </summary>
class CachedMessageInfo
{
	public UniqueId UniqueId;
	public MessageFlags Flags;
	public HashSet<string> UserFlags;
	public Envelope Envelope;
	public BodyPart Body;
}

/// <summary>
/// Resynchronize the cache with the remote IMAP folder.
/// </summary>
/// <param name="folder">The IMAP folder.</param>
/// <param name="cache">The local cache of message metadata.</param>
/// <param name="cachedUidValidity">The cached UIDVALIDITY value of the IMAP folder from a previous session.</param>
static void ResyncFolder (ImapFolder folder, List<CachedMessageInfo> cache, ref uint cachedUidValidity)
{
	IList<IMessageSummary> summaries;

	// Step 1: Open the folder.

	// Note: we only need read-only access to update our cache, but depending on
	// what you plan to do with the folder after resynchronizing, you may want
	// top open the folder in read-write mode instead.
	folder.Open (FolderAccess.ReadOnly);

	if (cache.Count > 0) {
		if (folder.UidValidity == cachedUidValidity) {
			// Step 2: Remove messages from our cache that no longer exist on the server.

			// get the full list of UIDs on the server...
			var all = folder.Search (SearchQuery.All);

			// remove any messages from our cache that no longer exist...
			for (int i = 0; i < cache.Count; i++) {
				if (!all.Contains (cache[i].UniqueId)) {
					cache.RemoveAt (i);
					i--;
				}
			}

			// Step 3: Sync any flag changes for our cached messages.

			// get a list of known uids... astute observers will note that an easy
			// optimization to make here would be to merge this loop with the above
			// loop.
			var known = new UniqueIdSet (SortOrder.Ascending);
			for (int i = 0; i < cache.Count; i++)
				known.Add (cache[i].UniqueId);

			// fetch the flags for our known messages...
			summaries = folder.Fetch (known, MessageSummaryItems.Flags);
			for (int i = 0; i < summaries.Count; i++) {
				// Note: the indexes should match up with our cache, but it wouldn't
				// hurt to add error checking to make sure. I'm not bothering to here
				// for simplicity reasons.
				cache[i].Flags = summaries[i].Flags.Value;
				cache[i].UserFlags = summaries[i].UserFlags;
			}
		} else {
			// The UIDVALIDITY of the folder has changed. This means that our entire
			// cache is obsolete. We need to clear our cache and start from scratch.
			cachedUidValidity = folder.UidValidity;
			cache.Clear ();
		}
	} else {
		// We have nothing cached, so just start from scratch.
		cachedUidValidity = folder.UidValidity;
	}

	// Step 4: Fetch the messages we don't already know about and add them to our cache.

	summaries = folder.Fetch (cache.Count, -1, MessageSummaryItems.UniqueId | MessageSummaryItems.Flags | MessageSummaryItems.Envelope | MessageSummaryItems.BodyStructure);
	for (int i = 0; i < summaries.Count; i++) {
		cache.Add (new CachedMessageInfo {
			UniqueId = summaries[i].UniqueId,
			Flags = summaries[i].Flags.Value,
			UserFlags = summaries[i].UserFlags,
			Envelope = summaries[i].Envelope,
			Body = summaries[i].Body
		});
	}

	// Tada! Now we are resynchronized with the server!
}
```

## SmtpClient

### <a name="SpecifiedPickupDirectory">Q: How can I send email to a SpecifiedPickupDirectory?</a>

Based on Microsoft's [referencesource](https://github.com/Microsoft/referencesource/blob/master/System/net/System/Net/mail/SmtpClient.cs#L401),
when `SmtpDeliveryMethod.SpecifiedPickupDirectory` is used, the `SmtpClient` saves the message to the
specified pickup directory location using a randomly generated filename based on
`Guid.NewGuid ().ToString () + ".eml"`, so to achieve the same results with MailKit, you could do something
like this:

```csharp
void SendToPickupDirectory (MimeMessage message, string pickupDirectory)
{
    do {
        var path = Path.Combine (pickupDirectory, Guid.NewGuid ().ToString () + ".eml");

        if (File.Exists (path))
            continue;

        try {
            using (var stream = new FileStream (path, FileMode.CreateNew)) {
                message.WriteTo (stream);
                return;
            }
        } catch (IOException) {
            // The file may have been created between our File.Exists() check and
            // our attempt to create the stream.
        }
    } while (true);
}
```
### <a name="SmtpRequestReadReceipt">Q: How can I request a notification when the message is read by the user?</a>

The first thing I need to make clear is that requesting a notification does not guarantee that you'll actually
get one. In order for you to receive a notification that the message was read by its recipient, the recipient's
mail client needs to know how to send such a notification *and* that the user has enabled it to do so.

That said, here's how you can request a notification when the recipient reads the message that has been sent:

```csharp
// Add the following header to tell the recipient's client that you want to receive a
// notification when the message has been read by the user.
message.Headers[HeaderId.DispositionNotificationTo] = new MailboxAddress ("My Name", "me@example.com").ToString (true);
```

For more information on this topic, read [rfc3798](https://tools.ietf.org/html/rfc3798).

### <a name="SmtpProcessReadReceipt">Q: How can I process a read receipt notification?</a>

A read receipt notification comes in the form of a MIME message with a top-level MIME part with a MIME-type
of `multipart/report` that has a `report-type` parameter with a value of `disposition-notification`.

You could check for this in code like this:

```csharp
var report = message.Body as MultipartReport;
if (report != null && report.ReportType.Equals ("disposition-notification", StringComparison.OrdinalIgnoreCase)) {
    // This is a read receipt notification.
}
```

The first part of the `multipart/report` will be a human-readable explanation of the notification.

The second part will have a MIME-type of `message/disposition-notification` and be represented by
a [MessageDispositionNotification](http://www.mimekit.net/docs/html/T_MimeKit_MessageDispositionNotification.htm).

This notification part will contain a list of header-like fields containing information about the
message that this notification is for such as the `Original-Message-Id`, `Original-Recipient`, etc.

```csharp
var notification = report[1] as MessageDispositionNotification;
if (notification != null) {
    // Get the Message-Id of the message this notification is for...
    var messageId = notification.Fields["Original-Message-Id"];
}
```
For more information on this topic, read [rfc3798](https://tools.ietf.org/html/rfc3798).
