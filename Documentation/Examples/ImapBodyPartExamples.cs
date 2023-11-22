//
// ImapBodyPartExamples.cs
//
// Author: Jeffrey Stedfast <jestedfa@microsoft.com>
//
// Copyright (c) 2013-2023 .NET Foundation and Contributors
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
// THE SOFTWARE.
//

using System;
using System.Collections;
using System.Collections.Generic;

using MimeKit;
using MailKit;
using MailKit.Search;
using MailKit.Security;
using MailKit.Net.Imap;

namespace MailKit.Examples {
	public static class ImapBodyPartExamples
	{
		#region GetBodyPartsByUniqueId
		public static void DownloadBodyAndAttachments (string baseDirectory)
		{
			using (var client = new ImapClient ()) {
				client.Connect ("imap.gmail.com", 993, SecureSocketOptions.SslOnConnect);

				client.Authenticate ("username", "password");

				client.Inbox.Open (FolderAccess.ReadOnly);

				// search for messages where the Subject header contains either "MimeKit" or "MailKit"
				var query = SearchQuery.SubjectContains ("MimeKit").Or (SearchQuery.SubjectContains ("MailKit"));
				var uids = client.Inbox.Search (query);

				// fetch summary information for the search results (we will want the UID and the BODYSTRUCTURE
				// of each message so that we can extract the text body and the attachments)
				var items = client.Inbox.Fetch (uids, MessageSummaryItems.UniqueId | MessageSummaryItems.BodyStructure);

				foreach (var item in items) {
					// determine a directory to save stuff in
					var directory = Path.Combine (baseDirectory, item.UniqueId.ToString ());

					// create the directory
					Directory.CreateDirectory (directory);

					// IMessageSummary.TextBody is a convenience property that finds the 'text/plain' body part for us
					var bodyPart = item.TextBody;

					if (bodyPart != null) {
						// download the 'text/plain' body part
						var plain = (TextPart) client.Inbox.GetBodyPart (item.UniqueId, bodyPart);

						// TextPart.Text is a convenience property that decodes the content and converts the result to
						// a string for us
						var text = plain.Text;

						File.WriteAllText (Path.Combine (directory, "body.txt"), text);
					}

					// IMessageSummary.HtmlBody is a convenience property that finds the 'text/html' body part for us
					bodyPart = item.HtmlBody;

					if (bodyPart != null) {
						// download the 'text/html' body part
						var html = (TextPart) client.Inbox.GetBodyPart (item.UniqueId, bodyPart);

						// TextPart.Text is a convenience property that decodes the content and converts the result to
						// a string for us
						var text = html.Text;

						File.WriteAllText (Path.Combine (directory, "body.html"), text);
					}

					// now iterate over all of the attachments and save them to disk
					foreach (var attachment in item.Attachments) {
						// download the attachment just like we did with the body
						var entity = client.Inbox.GetBodyPart (item.UniqueId, attachment);

						// attachments can be either message/rfc822 parts or regular MIME parts
						if (entity is MessagePart) {
							var rfc822 = (MessagePart) entity;

							var path = Path.Combine (directory, attachment.PartSpecifier + ".eml");

							rfc822.Message.WriteTo (path);
						} else {
							var part = (MimePart) entity;

							// default to using the sending client's suggested fileName value
							var fileName = attachment.FileName;

							if (string.IsNullOrEmpty (fileName)) {
								// the FileName wasn't defined, so generate one...
								if (!MimeTypes.TryGetExtension (attachment.ContentType.MimeType, out string extension))
									extension = ".dat";

								fileName = Guid.NewGuid ().ToString () + extension;
							}

							var path = Path.Combine (directory, fileName);

							// decode and save the content to a file
							using (var stream = File.Create (path))
								part.Content.DecodeTo (stream);
						}
					}
				}

				client.Disconnect (true);
			}
		}
		#endregion

		#region GetBodyPartsByUniqueIdAndSpecifier
		public static void DownloadBodyAndAttachments (string baseDirectory)
		{
			using (var client = new ImapClient ()) {
				client.Connect ("imap.gmail.com", 993, SecureSocketOptions.SslOnConnect);

				client.Authenticate ("username", "password");

				client.Inbox.Open (FolderAccess.ReadOnly);

				// search for messages where the Subject header contains either "MimeKit" or "MailKit"
				var query = SearchQuery.SubjectContains ("MimeKit").Or (SearchQuery.SubjectContains ("MailKit"));
				var uids = client.Inbox.Search (query);

				// fetch summary information for the search results (we will want the UID and the BODYSTRUCTURE
				// of each message so that we can extract the text body and the attachments)
				var items = client.Inbox.Fetch (uids, MessageSummaryItems.UniqueId | MessageSummaryItems.BodyStructure);

				foreach (var item in items) {
					// determine a directory to save stuff in
					var directory = Path.Combine (baseDirectory, item.UniqueId.ToString ());

					// create the directory
					Directory.CreateDirectory (directory);

					// IMessageSummary.TextBody is a convenience property that finds the 'text/plain' body part for us
					var bodyPart = item.TextBody;

					if (bodyPart != null) {
						// download the 'text/plain' body part

						// Note: In general, you should use `GetBodyPart(item.UniqueId, bodyPart)` instead if you have it available.
						// This particular overload of the GetBodyPart() method exists for convenience purposes where you already
						// know the body-part specifier string before-hand.
						var plain = (TextPart) client.Inbox.GetBodyPart (item.UniqueId, bodyPart.PartSpecifier);

						// TextPart.Text is a convenience property that decodes the content and converts the result to
						// a string for us
						var text = plain.Text;

						File.WriteAllText (Path.Combine (directory, "body.txt"), text);
					}

					// IMessageSummary.HtmlBody is a convenience property that finds the 'text/html' body part for us
					bodyPart = item.HtmlBody;

					if (bodyPart != null) {
						// download the 'text/html' body part

						// Note: In general, you should use `GetBodyPart(item.UniqueId, bodyPart)` instead if you have it available.
						// This particular overload of the GetBodyPart() method exists for convenience purposes where you already
						// know the body-part specifier string before-hand.
						var html = (TextPart) client.Inbox.GetBodyPart (item.UniqueId, bodyPart.PartSpecifier);

						// TextPart.Text is a convenience property that decodes the content and converts the result to
						// a string for us
						var text = html.Text;

						File.WriteAllText (Path.Combine (directory, "body.html"), text);
					}

					// now iterate over all of the attachments and save them to disk
					foreach (var attachment in item.Attachments) {
						// download the attachment just like we did with the body
						var entity = client.Inbox.GetBodyPart (item.UniqueId, attachment);

						// attachments can be either message/rfc822 parts or regular MIME parts
						if (entity is MessagePart) {
							var rfc822 = (MessagePart) entity;

							var path = Path.Combine (directory, attachment.PartSpecifier + ".eml");

							rfc822.Message.WriteTo (path);
						} else {
							var part = (MimePart) entity;

							// default to using the sending client's suggested fileName value
							var fileName = attachment.FileName;

							if (string.IsNullOrEmpty (fileName)) {
								// the FileName wasn't defined, so generate one...
								if (!MimeTypes.TryGetExtension (attachment.ContentType.MimeType, out string extension))
									extension = ".dat";

								fileName = Guid.NewGuid ().ToString () + extension;
							}

							var path = Path.Combine (directory, fileName);

							// decode and save the content to a file
							using (var stream = File.Create (path))
								part.Content.DecodeTo (stream);
						}
					}
				}

				client.Disconnect (true);
			}
		}
		#endregion

		#region GetBodyPartStreamsByUniqueId
		public static void CacheBodyParts (string baseDirectory)
		{
			using (var client = new ImapClient ()) {
				client.Connect ("imap.gmail.com", 993, SecureSocketOptions.SslOnConnect);

				client.Authenticate ("username", "password");

				client.Inbox.Open (FolderAccess.ReadOnly);

				// search for messages where the Subject header contains either "MimeKit" or "MailKit"
				var query = SearchQuery.SubjectContains ("MimeKit").Or (SearchQuery.SubjectContains ("MailKit"));
				var uids = client.Inbox.Search (query);

				// fetch summary information for the search results (we will want the UID and the BODYSTRUCTURE
				// of each message so that we can extract the text body and the attachments)
				var items = client.Inbox.Fetch (uids, MessageSummaryItems.UniqueId | MessageSummaryItems.BodyStructure);

				foreach (var item in items) {
					// determine a directory to save stuff in
					var directory = Path.Combine (baseDirectory, item.UniqueId.ToString ());

					// create the directory
					Directory.CreateDirectory (directory);

					// now iterate over all of the body parts and save them to disk
					foreach (var bodyPart in item.BodyParts) {
						// cache the raw body part MIME just like we did with the body
						using (var stream = client.Inbox.GetStream (item.UniqueId, bodyPart)) {
							var path = Path.Combine (directory, bodyPart.PartSpecifier);

							using (var output = File.Create (path))
								stream.CopyTo (output);
						}
					}
				}

				client.Disconnect (true);
			}
		}
		#endregion

		#region GetBodyPartStreamsByUniqueIdAndSpecifier
		public static void SaveAttachments (string baseDirectory)
		{
			using (var client = new ImapClient ()) {
				client.Connect ("imap.gmail.com", 993, SecureSocketOptions.SslOnConnect);

				client.Authenticate ("username", "password");

				client.Inbox.Open (FolderAccess.ReadOnly);

				// search for messages where the Subject header contains either "MimeKit" or "MailKit"
				var query = SearchQuery.SubjectContains ("MimeKit").Or (SearchQuery.SubjectContains ("MailKit"));
				var uids = client.Inbox.Search (query);

				// fetch summary information for the search results (we will want the UID and the BODYSTRUCTURE
				// of each message so that we can extract the text body and the attachments)
				var items = client.Inbox.Fetch (uids, MessageSummaryItems.UniqueId | MessageSummaryItems.BodyStructure);

				foreach (var item in items) {
					// determine a directory to save stuff in
					var directory = Path.Combine (baseDirectory, item.UniqueId.ToString ());

					// create the directory
					Directory.CreateDirectory (directory);

					// now iterate over all of the attachments and decode/save the content to disk
					foreach (var attachment in item.Attachments) {
						// default to using the sending client's suggested fileName value
						string fileName = attachment.FileName;

						if (string.IsNullOrEmpty (fileName)) {
							// the FileName wasn't defined, so generate one...
							if (!MimeTypes.TryGetExtension (attachment.ContentType.MimeType, out string extension))
								extension = ".dat";

							fileName = Guid.NewGuid ().ToString () + extension;
						}

						// we'll need the Content-Transfer-Encoding value so that we can decode it...
						ContentEncoding encoding;

						if (string.IsNullOrEmpty (attachment.ContentTransferEncoding) || !MimeUtils.TryParse (attachment.ContentTransferEncoding, out encoding))
							encoding = ContentEncoding.Default;

						// if all we want is the content (rather than the entire MIME part including the headers), then
						// we want the ".TEXT" section of the part
						using (var stream = client.Inbox.GetStream (item.UniqueId, attachment.PartSpecifier + ".TEXT")) {
							// wrap the attachment content in a MimeContent object to help us decode it
							using (var content = new MimeContent (stream, encoding)) {
								var path = Path.Combine (directory, fileName);

								// decode the attachment content to the file stream
								using (var output = File.Create (path))
									content.DecodeTo (output);
							}
						}
					}
				}

				client.Disconnect (true);
			}
		}
		#endregion
	}
}
