foreach (var attachment in message.Attachments.OfType<MimePart> ()) {
	var fileName = attachment.FileName;

	using (var stream = File.Create (fileName)) {
		attachment.ContentObject.DecodeTo (stream);
	}
}
