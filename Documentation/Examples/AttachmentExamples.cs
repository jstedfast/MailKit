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
				attachment.ContentObject.DecodeTo (stream);
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
			#endregion SaveAttachments
		}
	}
}
