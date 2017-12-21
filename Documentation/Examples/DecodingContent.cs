// This will get the name of the file as specified by the sending mail client.
// Note: this value *may* be null, so you'll want to handle that case in your code.
var fileName = part.FileName;

using (var stream = File.Create (fileName)) {
    part.Content.DecodeTo (stream);
}
