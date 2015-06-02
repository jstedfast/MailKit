static void HandleMimeEntity (MimeEntity entity)
{
	var multipart = entity as Multipart;

	if (multipart != null) {
		for (int i = 0; i < multipart.Count; i++)
			HandleMimeEntity (multipart[i]);
		return;
	}

	var rfc822 = entity as MessagePart;

	if (rfc822 != null) {
		var message = rfc822.Message;

		HandleMimeEntity (message.Body);
		return;
	}

	var part = (MimePart) entity;

    // do something with the MimePart, such as save content to disk
}