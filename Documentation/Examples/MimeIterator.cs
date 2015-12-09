var attachments = new List<MimePart> ();
var multiparts = new List<Multipart> ();

using (var iter = new MimeIterator (message)) {
    // collect our list of attachments and their parent multiparts
    while (iter.MoveNext ()) {
        var multipart = iter.Parent as Multipart;
        var part = iter.Current as MimePart;

        if (multipart != null && part != null && part.IsAttachment) {
            // keep track of each attachment's parent multipart
            multiparts.Add (multipart);
            attachments.Add (part);
        }
    }
}

// now remove each attachment from its parent multipart...
for (int i = 0; i < attachments.Count; i++)
    multiparts[i].Remove (attachments[i]);
