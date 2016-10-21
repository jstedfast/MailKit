# Frequently Asked Questions

## Question Index

* [Are MimeKit and MailKit completely free? Can I use them in my proprietary product(s)?](#CompletelyFree)
* [How do I create a message with attachments?](#CreateAttachments)
* [How do I get the main body of a message?](#MessageBody)
* [How do I tell if a message has attachments?](#HasAttachments)
* [How do I save attachments?](#SaveAttachments)
* [How do I get the email addresses in the From, To, and Cc headers?](#AddressHeaders)
* [Why doesn't the MimeMessage class implement ISerializable so that I can serialize a message to disk and read it back later?](#Serialize)
* [Why do attachments with unicode filenames appear as "ATT0####.dat" in Outlook?](#UntitledAttachments)
* [How do I decrypt PGP messages that are embedded in the main message text?](#DecryptInlinePGP)
* [How do I reply to a message using MimeKit and MailKit?](#Reply)
* [How can I get a protocol log for IMAP, POP3, or SMTP to see what is going wrong?](#ProtocolLog)
* [Why doesn't MailKit find some of my GMail POP3 or IMAP messages?](#GMailHiddenMessages)
* [How can I log in to a GMail account using OAuth 2.0?](#GMailOAuth2)
* [How can I search for messages delivered between two dates?](#SearchBetween2Dates)
* [What does "The ImapClient is currently busy processing a command." mean?](#ImapClientBusy)
* [Why do I get InvalidOperationException: "The folder is not currently open."?](#FolderNotOpenException)
* [ImapFolder.MoveTo() does not move the message out of the source folder. Why not?](#MoveDoesNotMove)
* [How can I mark messages as read using IMAP?](#MarkAsRead)
* [How can I send email to the SpecifiedPickupDirectory?](#SpecifiedPickupDirectory)
* [How can I request a notification when the message is read by the user?](#RequestReadReceipt)
* [How can I process a read receipt notification?](#ProcessReadReceipt)

### <a name="CompletelyFree">Are MimeKit and MailKit completely free? Can I use them in my proprietary product(s)?</a>

Yes. MimeKit and MailKit are both completely free and open source. They are both covered under the
[MIT](https://opensource.org/licenses/MIT) license.

### <a name="CreateAttachments">How do I create a message with attachments?</a>

To construct a message with attachments, the first thing you'll need to do is create a `multipart/mixed`
container which you'll then want to add the message body to first. Once you've added the body, you can
then add MIME parts to it that contain the content of the files you'd like to attach, being sure to set
the `Content-Disposition` header value to attachment. You'll probably also want to set the `filename`
parameter on the `Content-Disposition` header as well as the `name` parameter on the `Content-Type`
header. The most convenient way to do this is to simply use the
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
    ContentObject = new ContentObject (File.OpenRead (path), ContentEncoding.Default),
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

For more information, see [Creating Messages](http://www.mimekit.net/docs/html/CreatingMessages.htm).

### <a name="MessageBody">How do I get the main body of a message?</a>

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

For more information, see [Working with Messages](http://www.mimekit.net/docs/html/WorkingWithMessages.htm).

### <a name="HasAttachments">How do I tell if a message has attachments?</a>

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
                image.ContentObject.DecodeTo (output);
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

### <a name="SaveAttachments">How do I save attachments?</a>

If you've already got a [MimePart](http://www.mimekit.net/docs/html/T_MimeKit_MimePart.htm) that represents
the attachment that you'd like to save, here's how you might save it:

```csharp
using (var stream = File.Create (fileName))
    attachment.ContentObject.DecodeTo (stream);
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
            
            part.ContentObject.DecodeTo (stream);
        }
    }
}
```

### <a name="AddressHeaders">How do I get the email addresses in the From, To, and Cc headers?</a>

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
header, you can simply do something like this:

```csharp
foreach (var mailbox in message.To.Mailboxes)
    Console.WriteLine ("{0}'s email address is {1}", mailbox.Name, mailbox.Address);
```

### <a name="Serialize">Why doesn't the MimeMessage class implement ISerializable so that I can serialize a message to disk and read it back later?</a>

The MimeKit API was designed to use the existing MIME format for serialization. In light of this, the ability
to use the .NET serialization API and format did not make much sense to support.

You can easily serialize a MimeMessage to a stream using the
[WriteTo](http://www.mimekit.net/docs/html/Overload_MimeKit_MimeMessage_WriteTo.htm) methods.

### <a name="UntitledAttachments">Why do attachments with unicode filenames appear as "ATT0####.dat" in Outlook?</a>

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

### <a name="DecryptInlinePGP">How do I decrypt PGP messages that are embedded in the main message text?</a>

Some PGP-enabled mail clients, such as Thunderbird, embed encrypted PGP blurbs within the text/plain body
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

There are actually 2 variants of this methods:

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
        text.ContentObject.DecodeTo (memory);
        memory.Position = 0;

        using (var ctx = new MyGnuPGContext ()) {
            return ctx.GetDecryptedStream (memory);
        }
    }
}
```

What you do with that decrypted stream is up to you. It's up to you to figure out what the decrypted content is
(is it text? a jpeg image? a video?) and how to display it to the user.

### <a name="Reply">How do I reply to a message using MimeKit?</a>

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

### <a name="ProtocolLog">How can I get a protocol log for IMAP, POP3, or SMTP to see what is going wrong?</a>

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

bool result = await credential.RequestAccessTokenAsync (CancellationToken.None);

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

### <a name="FolderNotOpenException">Why do I get InvalidOperationException: "The folder is not currently open."?</a>

If you get this exception, it's probably because you thought you had to open the destination folder that you
passed as an argument to one of the
[CopyTo](http://www.mimekit.net/docs/html/Overload_MailKit_MailFolder_CopyTo.htm) or
[MoveTo](http://www.mimekit.net/docs/html/Overload_MailKit_MailFolder_MoveTo.htm) methods. When you opened
that destination folder, you also inadvertantly closed the source folder which is why you are getting this
exception.

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

### <a name="SpecifiedPickupDirectory">How can I send email to a SpecifiedPickupDirectory?</a>

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
### <a name="RequestReadReceipt">How can I request a notification when the message is read by the user?</a>

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

### <a name="ProcessReadReceipt">How can I process a read receipt notification?</a>

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
