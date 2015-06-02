// Load every message from a Unix mbox
var parser = new MimeParser (stream, MimeFormat.Mbox);
while (!parser.IsEndOfStream) {
    var message = parser.ParseMessage ();

    // do something with the message
}
