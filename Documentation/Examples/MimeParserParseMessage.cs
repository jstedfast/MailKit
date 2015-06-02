// Load a MimeMessage from a stream
var parser = new MimeParser (stream, MimeFormat.Entity);
var message = parser.ParseMessage ();
