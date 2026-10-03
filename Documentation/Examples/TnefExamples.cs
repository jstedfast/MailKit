//
// TnefExamples.cs
//
// Author: Jeffrey Stedfast <jestedfa@microsoft.com>
//
// Copyright (c) 2013-2026 .NET Foundation and Contributors
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
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;

using MimeKit;
using MimeKit.IO;
using MimeKit.Tnef;

namespace MimeKit.Examples
{
	public static class TnefExamples
	{
		#region ConvertToMime
		public static MimeMessage ConvertToMime (TnefPart part)
		{
			// Load the content of the application/ms-tnef (winmail.dat) part.
			using (var tnef = part.LoadTnefMessage ()) {
				// By default, embedded messages are converted into nested TnefParts. Set ConvertEmbeddedMessages
				// to convert them into message/rfc822 MessageParts instead.
				var options = new TnefConversionOptions { ConvertEmbeddedMessages = true };

				// Convert the TNEF message into a MimeMessage. The converted message does not depend on the
				// TnefMessage, so it may still be used after the TnefMessage has been disposed.
				var result = tnef.ConvertToMime (options);

				// Anything that could not be represented in MIME is reported as a conversion loss.
				foreach (var loss in result.Losses)
					Console.WriteLine ("TNEF conversion loss: {0}: {1}", loss.Kind, loss.Description);

				// Note: Disposing the result would also dispose result.Message, so don't dispose it
				// if you want to keep using the message.
				return result.Message;
			}
		}
		#endregion

		#region ConvertToMimeAsync
		public static async Task<MimeMessage> ConvertToMimeAsync (TnefPart part, CancellationToken cancellationToken = default)
		{
			using (var tnef = await part.LoadTnefMessageAsync (null, cancellationToken)) {
				// Note: Once the TnefMessage has been loaded, all of its content is in memory, so
				// ConvertToMime () does not need an async counterpart.
				var result = tnef.ConvertToMime (null, cancellationToken);

				return result.Message;
			}
		}
		#endregion

		#region ExtractAttachments
		public static void ExtractAttachments (MimeMessage message, string outputDirectory)
		{
			foreach (var part in message.BodyParts.OfType<TnefPart> ()) {
				using (var tnef = part.LoadTnefMessage ())
					SaveAttachments (tnef, outputDirectory);
			}
		}

		static void SaveAttachments (TnefMessage tnef, string outputDirectory)
		{
			foreach (var attachment in tnef.Attachments) {
				if (attachment.IsEmbeddedMessage) {
					// An embedded message is a TNEF message in its own right, which may have its own attachments.
					using (var embedded = attachment.LoadEmbeddedMessage ())
						SaveAttachments (embedded, outputDirectory);
					continue;
				}

				if (!attachment.HasContent)
					continue;

				// Note: The file name comes from an untrusted source, so sanitize it before using it as a path.
				var fileName = Path.GetFileName (attachment.FileName);

				if (string.IsNullOrEmpty (fileName))
					fileName = "attachment.dat";

				using (var content = attachment.OpenRead ())
				using (var output = File.Create (Path.Combine (outputDirectory, fileName)))
					content.CopyTo (output);
			}
		}
		#endregion

		#region ReadProperties
		public static void PrintMessageProperties (string fileName)
		{
			using (var stream = File.OpenRead (fileName))
			using (var tnef = TnefMessage.Load (stream)) {
				// The most commonly used properties have convenience accessors.
				Console.WriteLine ("Message-Class: {0}", tnef.MessageClass);
				Console.WriteLine ("Subject: {0}", tnef.Subject);
				Console.WriteLine ("Message-Id: {0}", tnef.InternetMessageId);

				// Any other property may be looked up by its tag. Note that a string lookup matches both the
				// String8 and the Unicode variant of the property tag.
				var sender = tnef.Properties.GetString (TnefPropertyTag.SenderEmailAddressW);
				var submitted = tnef.Properties.GetDateTime (TnefPropertyTag.ClientSubmitTime);
				var importance = tnef.Properties.GetInt32 (TnefPropertyTag.Importance);

				Console.WriteLine ("Sender: {0}", sender);
				Console.WriteLine ("Submitted: {0} (UTC)", submitted);
				Console.WriteLine ("Importance: {0}", importance);

				// Named properties are looked up by their TnefNameId.
				if (tnef.Properties.TryGetValue (TnefNameId.Location, out var location) && location.TryGetString (out var where))
					Console.WriteLine ("Location: {0}", where);

				// Multi-valued properties have more than one value.
				if (tnef.Properties.TryGetValue (TnefNameId.Keywords, out var keywords) && keywords.TryGetValues<string> (out var categories))
					Console.WriteLine ("Categories: {0}", string.Join (", ", categories));

				// Or simply enumerate every property.
				foreach (var property in tnef.Properties) {
					var name = property.Name.HasValue ? property.Name.Value.ToString () : property.Tag.Id.ToString ();
					var value = property.Value;

					if (value is byte[] bytes)
						value = string.Format ("<{0} bytes>", bytes.Length);
					else if (value is Array array)
						value = string.Join (", ", array.Cast<object> ());

					Console.WriteLine ("{0} ({1}) = {2}", name, property.PropertyType, value);
				}
			}
		}
		#endregion

		#region ReadRecipients
		public static void PrintRecipients (TnefMessage tnef)
		{
			foreach (var recipient in tnef.Recipients) {
				Console.WriteLine ("{0}: {1} <{2}> ({3})", recipient.RecipientType, recipient.DisplayName,
					recipient.EmailAddress, recipient.AddressType);

				// Every property of the recipient is also available.
				var smtpAddress = recipient.Properties.GetString (TnefPropertyTag.SmtpAddressW);

				if (smtpAddress != null)
					Console.WriteLine ("    SMTP address: {0}", smtpAddress);
			}
		}
		#endregion

		#region ReadBodies
		public static string GetBodyText (TnefMessage tnef, out TnefMessageBodyFormat format)
		{
			// A TNEF message may have a plain-text body, an HTML body and/or a compressed RTF body.
			if (tnef.HtmlBody != null) {
				format = tnef.HtmlBody.Format;
				return tnef.HtmlBody.GetText ();
			}

			if (tnef.RtfBody != null) {
				format = tnef.RtfBody.Format;

				// OpenDecodedRead () decompresses the RTF; OpenRead () would return the compressed bytes.
				using (var rtf = tnef.RtfBody.OpenDecodedRead ())
				using (var reader = new StreamReader (rtf, Encoding.ASCII))
					return reader.ReadToEnd ();
			}

			if (tnef.TextBody != null) {
				format = tnef.TextBody.Format;
				return tnef.TextBody.GetText ();
			}

			format = TnefMessageBodyFormat.Text;

			return string.Empty;
		}
		#endregion

		#region DecompressRtf
		public static void DecompressRtf (byte[] rtfCompressed, Stream output)
		{
			// The RtfCompressedToRtf filter decompresses the value of a PidTagRtfCompressed property ([MS-OXRTFCP]),
			// such as the value read by TnefPropertyReader.ReadValueAsBytes () or the raw content of a
			// TnefMessage's RtfBody (which TnefMessageBody.OpenDecodedRead () can also decompress for you).
			var filter = new RtfCompressedToRtf ();

			using (var filtered = new FilteredStream (output)) {
				filtered.Add (filter);
				filtered.Write (rtfCompressed, 0, rtfCompressed.Length);
				filtered.Flush ();
			}

			if (!filter.IsValidCrc32)
				Console.WriteLine ("The compressed RTF has an invalid CRC32 checksum.");
		}
		#endregion

		#region TnefReader
		public static void DumpTnef (Stream stream)
		{
			using (var reader = new TnefReader (stream, null, leaveOpen: true))
				DumpAttributes (reader);
		}

		static void DumpAttributes (TnefReader reader)
		{
			var indent = new string (' ', reader.Depth * 4);

			while (reader.Read ()) {
				Console.WriteLine ("{0}{1} {2} ({3} bytes) @ offset {4}", indent, reader.Level, reader.Tag, reader.Length, reader.StreamOffset);

				switch (reader.Tag) {
				case TnefAttributeTag.MessageClass:
				case TnefAttributeTag.Subject:
					Console.WriteLine ("{0}    {1}", indent, reader.ReadValueAsString ());
					break;
				case TnefAttributeTag.DateSent:
				case TnefAttributeTag.DateReceived:
					Console.WriteLine ("{0}    {1}", indent, reader.ReadValueAsDateTime ());
					break;
				case TnefAttributeTag.RecipientTable:
					var recipients = reader.GetPropertyReader ();

					// The recipient table is a list of rows, each of which has its own list of properties.
					while (recipients.ReadNextRow ()) {
						Console.WriteLine ("{0}    Recipient:", indent);
						DumpProperties (recipients, indent + "        ");
					}
					break;
				case TnefAttributeTag.MapiProperties:
				case TnefAttributeTag.Attachment:
					DumpProperties (reader.GetPropertyReader (), indent + "    ");
					break;
				}

				// Note: There is no need to read the value of every attribute; any value that is
				// not read is skipped (and its checksum is still verified) by the next Read ().
			}
		}

		static void DumpProperties (TnefPropertyReader prop, string indent)
		{
			while (prop.ReadNextProperty ()) {
				var name = prop.Name.HasValue ? prop.Name.Value.ToString () : prop.Tag.Id.ToString ();

				if (prop.IsEmbeddedMessage) {
					Console.WriteLine ("{0}{1} = <embedded message>", indent, name);

					using (var embedded = prop.OpenEmbeddedMessage ())
						DumpAttributes (embedded);
					continue;
				}

				if (prop.ValueCount == 0) {
					Console.WriteLine ("{0}{1} = <no values>", indent, name);
					continue;
				}

				// Note: The reader is already positioned on the first value of the property.
				do {
					var value = prop.ReadValue ();

					if (value is byte[] bytes)
						Console.WriteLine ("{0}{1} = <{2} bytes>", indent, name, bytes.Length);
					else
						Console.WriteLine ("{0}{1} = {2}", indent, name, value);
				} while (prop.ReadNextValue ());
			}
		}
		#endregion

		#region TnefReaderAsync
		public static async Task<string> GetSubjectAsync (Stream stream, CancellationToken cancellationToken = default)
		{
			string subject = null;

			using (var reader = new TnefReader (stream, null, leaveOpen: true)) {
				while (await reader.ReadAsync (cancellationToken)) {
					if (reader.Level != TnefAttributeLevel.Message)
						break;

					if (reader.Tag == TnefAttributeTag.Subject) {
						subject ??= await reader.ReadValueAsStringAsync (cancellationToken);
					} else if (reader.Tag == TnefAttributeTag.MapiProperties) {
						var prop = reader.GetPropertyReader ();

						while (await prop.ReadNextPropertyAsync (cancellationToken)) {
							// Note: PidTagSubject, if present, takes precedence over the legacy attSubject attribute.
							if (prop.Tag.Id == TnefPropertyId.Subject && prop.ValueCount > 0)
								subject = await prop.ReadValueAsStringAsync (cancellationToken);
						}
					}
				}
			}

			return subject;
		}
		#endregion

		#region ComplianceLogger
		class TnefComplianceCollector : ITnefComplianceLogger
		{
			public List<TnefComplianceIssue> Issues { get; } = new List<TnefComplianceIssue> ();

			public void Log (in TnefComplianceIssue issue)
			{
				Issues.Add (issue);
			}
		}

		class StrictTnefComplianceLogger : ITnefComplianceLogger
		{
			public void Log (in TnefComplianceIssue issue)
			{
				// Reject anything that is likely to cause real problems; tolerate the rest.
				if (issue.Severity >= MimeComplianceSeverity.Major)
					throw new TnefException (issue.Violation, issue.Description);
			}
		}

		public static TnefMessage LoadAndReportIssues (Stream stream)
		{
			var collector = new TnefComplianceCollector ();
			TnefMessage tnef;

			// The reader never throws for malformed TNEF; it recovers as best it can and reports each problem
			// to the ComplianceLogger. MaxComplianceIssuesPerViolation limits how many times each kind of
			// violation is reported.
			using (var reader = new TnefReader (stream, null, leaveOpen: true) { ComplianceLogger = collector, MaxComplianceIssuesPerViolation = 16 })
				tnef = TnefMessage.Load (reader);

			foreach (var issue in collector.Issues)
				Console.WriteLine ("{0} ({1}) @ offset {2}: {3}", issue.Violation, issue.Severity, issue.StreamOffset, issue.Description);

			return tnef;
		}

		public static TnefMessage LoadStrict (Stream stream)
		{
			using (var reader = new TnefReader (stream, null, leaveOpen: true) { ComplianceLogger = new StrictTnefComplianceLogger () })
				return TnefMessage.Load (reader);
		}
		#endregion

		#region Limits
		public static TnefMessage LoadUntrusted (TnefPart part)
		{
			// TnefMessage buffers the bodies and attachments in memory, so set limits that are appropriate
			// for the application when the TNEF comes from an untrusted source.
			var options = new TnefOptions {
				// The codepage to use when the TNEF stream does not specify one.
				DefaultCodepage = 1252,
				MaxNestingDepth = 8,
				MaxAttachments = 256,
				MaxPropertyValueLength = 16 * 1024 * 1024,
				MaxTotalDataBytes = 64L * 1024 * 1024
			};

			return part.LoadTnefMessage (options);
		}
		#endregion

		#region TnefWriter
		public static void WriteTnef (Stream output, byte[] rtf, string attachmentFileName, Stream attachmentContent)
		{
			// The TnefWriter writes the TNEF signature, attTnefVersion and attOemCodepage itself, computes
			// every length, count, checksum and padding, and throws InvalidOperationException if attributes
			// are written in an order that [MS-OXTNEF] does not allow.
			using (var writer = new TnefWriter (output, 1252, 0, leaveOpen: true)) {
				// Message-level attributes come first...
				writer.WriteAttribute (TnefAttributeTag.MessageClass, "IPM.Note");

				// ...followed by the recipient table, which has a row of properties for each recipient...
				using (var recipients = writer.OpenPropertyWriter (TnefAttributeTag.RecipientTable)) {
					recipients.BeginRow ();
					recipients.WritePropertyTag (TnefPropertyTag.RecipientType);
					recipients.WriteValue ((int) TnefRecipientType.To);
					recipients.WritePropertyTag (TnefPropertyTag.DisplayNameW);
					recipients.WriteValue ("Alice");
					recipients.WritePropertyTag (TnefPropertyTag.AddrtypeW);
					recipients.WriteValue ("SMTP");
					recipients.WritePropertyTag (TnefPropertyTag.EmailAddressW);
					recipients.WriteValue ("alice@example.com");
				}

				// ...and the message's MAPI properties, which must be the last message-level attribute.
				using (var properties = writer.OpenPropertyWriter (TnefAttributeTag.MapiProperties)) {
					properties.WritePropertyTag (TnefPropertyTag.SubjectW);
					properties.WriteValue ("Quarterly report");

					// Note: PT_SYSTIME property values are always in UTC.
					properties.WritePropertyTag (TnefPropertyTag.ClientSubmitTime);
					properties.WriteValue (DateTime.UtcNow);

					// The RTF body is compressed as it is written.
					properties.WritePropertyTag (TnefPropertyTag.RtfCompressed);
					using (var body = properties.OpenRtfCompressedStream ())
						body.Write (rtf, 0, rtf.Length);

					// Named properties are assigned property ids (0x8000 and up) by the writer.
					properties.WritePropertyTag (TnefNameId.Keywords, TnefPropertyType.Unicode | TnefPropertyType.MultiValued);
					properties.WriteValue ("finance");
					properties.WriteValue ("q3");
				}

				// Each attachment begins with an attAttachRendData attribute: the attachment type (1 = file),
				// its position in the body (-1 = not rendered), its width and height, and its flags.
				var renderData = new byte[] { 0x01, 0x00, 0xFF, 0xFF, 0xFF, 0xFF, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 };
				writer.WriteAttribute (TnefAttributeTag.AttachRenderData, renderData);

				// ...and ends with its MAPI properties.
				using (var properties = writer.OpenPropertyWriter (TnefAttributeTag.Attachment)) {
					properties.WritePropertyTag (TnefPropertyTag.AttachMethod);
					properties.WriteValue ((int) TnefAttachMethod.ByValue);
					properties.WritePropertyTag (TnefPropertyTag.AttachLongFilenameW);
					properties.WriteValue (attachmentFileName);
					properties.WritePropertyTag (TnefPropertyTag.AttachDataBin);

					using (var content = properties.OpenValueStream ())
						attachmentContent.CopyTo (content);
				}

				writer.Flush ();
			}
		}
		#endregion

		#region TnefWriterEmbeddedMessage
		public static void WriteEmbeddedMessage (TnefWriter writer, TnefMessage message)
		{
			var renderData = new byte[] { 0x01, 0x00, 0xFF, 0xFF, 0xFF, 0xFF, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 };

			writer.WriteAttribute (TnefAttributeTag.AttachRenderData, renderData);

			using (var properties = writer.OpenPropertyWriter (TnefAttributeTag.Attachment)) {
				properties.WritePropertyTag (TnefPropertyTag.AttachMethod);
				properties.WriteValue ((int) TnefAttachMethod.EmbeddedMessage);

				// An embedded message is the value of a PidTagAttachDataObject property.
				properties.WritePropertyTag (TnefPropertyTag.AttachDataObj);

				using (var embedded = properties.OpenEmbeddedMessage ()) {
					if (message.MessageClass != null)
						embedded.WriteAttribute (TnefAttributeTag.MessageClass, message.MessageClass);

					// Copy the properties of an existing TnefMessage unchanged.
					using (var embeddedProperties = embedded.OpenPropertyWriter (TnefAttributeTag.MapiProperties)) {
						foreach (var property in message.Properties)
							embeddedProperties.WriteProperty (property);
					}
				}
			}
		}
		#endregion
	}
}