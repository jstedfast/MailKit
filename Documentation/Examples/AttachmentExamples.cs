using System;

using MimeKit;

namespace MimeKit.Examples
{
	public static class AttachmentExamples
	{
		public static void SaveMimePart (MimePart attachment, string fileName)
		{
			#region SaveMimePart
			using (var stream = File.Create (fileName))
				attachment.Content.DecodeTo (stream);
			#endregion SaveMimePart
		}

		public static void SaveMimePart (MessagePart attachment, string fileName)
		{
			#region SaveMessagePart
			using (var stream = File.Create (fileName))
				attachment.Message.WriteTo (stream);
			#endregion SaveMessagePart
		}

		public static void SaveAttachments (MimeMessage message)
		{
			#region SaveAttachments
			foreach (var attachment in message.Attachments) {
				if (attachment is MessagePart) {
					var fileName = attachment.ContentDisposition?.FileName :
						(attachment.ContentType.Name ?? "attached.eml");
					var rfc822 = (MessagePart) attachment;

					rfc822.Message.WriteTo (stream);
				} else {
					var part = (MimePart) attachment;
					var fileName = part.FileName;

					using (var stream = File.Create (fileName))
						part.Content.DecodeTo (stream);
				}
			}
			#endregion SaveAttachments
		}

		public static void SaveAttachments (MimeMessage message)
		{
			#region SaveBodyParts
			foreach (var bodyPart in message.BodyParts) {
				if (!bodyPart.IsAttachment)
					continue;

				if (bodyPart is MessagePart) {
					var fileName = bodyPart.ContentDisposition?.FileName :
						(bodyPart.ContentType.Name ?? "attached.eml");
					var rfc822 = (MessagePart) bodyPart;

					rfc822.Message.WriteTo (stream);
				} else {
					var part = (MimePart) attachment;
					var fileName = part.FileName;

					using (var stream = File.Create (fileName))
						part.Content.DecodeTo (stream);
				}
			}
			#endregion SaveBodyParts
		}
	}
}
