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
