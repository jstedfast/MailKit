//
// BinaryMessageSummaryIndex.cs
//
// Author: Jeffrey Stedfast <jestedfa@microsoft.com>
//
// Copyright (c) 2013-2024 .NET Foundation and Contributors
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
using System.Diagnostics;
using System.Collections.Generic;

using MimeKit;
using MimeKit.Utils;
using MailKit.Search;

namespace MailKit.Caching {
	public class BinaryMessageSummaryIndex : IMessageSummaryIndex
	{
		static readonly FormatOptions formatOptions;
		const string TempIndexFileName = "messages.tmp";
		const string IndexFileName = "messages.index";
		const int ExpectedVersion = 1;

		string baseCacheDir, cacheDir;
		List<MessageSummary> messages;
		IndexHeader header;

		static BinaryMessageSummaryIndex ()
		{
			formatOptions = FormatOptions.Default.Clone ();
			formatOptions.NewLineFormat = NewLineFormat.Dos;
		}

		public BinaryMessageSummaryIndex (string baseCacheDir, string folderFullName)
		{
			this.baseCacheDir = baseCacheDir;
			this.cacheDir = Path.Combine (baseCacheDir, EncodeFolderName (folderFullName));
		}

		/// <summary>
		/// Encode a folder specifier so that it can be used as part of a file system temp.
		/// </summary>
		/// <param specifier="folder">The folder.</param>
		/// <returns>An encoded specifier that is safe to be used in a file system temp.</returns>
		static string EncodeFolderName (string fullName)
		{
			var builder = new StringBuilder ();

			for (int i = 0; i < fullName.Length; i++) {
				switch (fullName[i]) {
				case '%': builder.Append ("%25"); break;
				case '/': builder.Append ("%2F"); break;
				case ':': builder.Append ("%3A"); break;
				case '\\': builder.Append ("%5C"); break;
				default: builder.Append (fullName[i]); break;
				}
			}

			return builder.ToString ();
		}

		class IndexHeader
		{
			public int Version;

			public uint UidNext;
			public uint UidValidity;
			public ulong HighestModSeq;
		}

		public uint? UidNext { get { return header?.UidNext; } }

		public uint? UidValidity { get { return header?.UidValidity; } }

		public ulong? HighestModSeq { get { return header?.HighestModSeq; } }

		public int Count { get { return messages?.Count ?? 0; } }

		public bool IsDirty { get; private set; }

		static DateTimeOffset? LoadDateTimeOffset (BinaryReader reader)
		{
			var ticks = reader.ReadInt64 ();

			if (ticks == 0)
				return null;

			var offset = reader.ReadInt32 ();

			return new DateTimeOffset (ticks, TimeSpan.FromSeconds (offset));
		}

		static void SaveDateTimeOffset (BinaryWriter writer, DateTimeOffset? dateTime)
		{
			if (dateTime.HasValue) {
				writer.Write (dateTime.Value.Ticks);
				writer.Write ((int) dateTime.Value.Offset.TotalSeconds);
			} else {
				writer.Write (0L);
			}
		}

		static void LoadInternetAddressList (BinaryReader reader, InternetAddressList list)
		{
			var value = reader.ReadString ();

			if (string.IsNullOrEmpty (value))
				return;

			list.AddRange (InternetAddressList.Parse (value));
		}

		static void SaveInternetAddressList (BinaryWriter writer, InternetAddressList list)
		{
			writer.Write (list.ToString ());
		}

		static Envelope LoadEnvelope (BinaryReader reader)
		{
			var envelope = new Envelope ();

			envelope.Date = LoadDateTimeOffset (reader);
			envelope.Subject = reader.ReadString ();
			LoadInternetAddressList (reader, envelope.From);
			LoadInternetAddressList (reader, envelope.Sender);
			LoadInternetAddressList (reader, envelope.ReplyTo);
			LoadInternetAddressList (reader, envelope.To);
			LoadInternetAddressList (reader, envelope.Cc);
			LoadInternetAddressList (reader, envelope.Bcc);
			envelope.InReplyTo = reader.ReadString ();
			envelope.MessageId = reader.ReadString ();

			return envelope;
		}

		static void SaveEnvelope (BinaryWriter writer, Envelope envelope)
		{
			SaveDateTimeOffset (writer, envelope.Date);
			writer.Write (envelope.Subject);
			SaveInternetAddressList (writer, envelope.From);
			SaveInternetAddressList (writer, envelope.Sender);
			SaveInternetAddressList (writer, envelope.ReplyTo);
			SaveInternetAddressList (writer, envelope.To);
			SaveInternetAddressList (writer, envelope.Cc);
			SaveInternetAddressList (writer, envelope.Bcc);
			writer.Write (envelope.InReplyTo);
			writer.Write (envelope.MessageId);
		}

		static string[] LoadStrings (BinaryReader reader)
		{
			int n = reader.ReadInt32 ();
			var array = new string[n];

			for (int i = 0; i < n; i++)
				array[i] = reader.ReadString ();

			return array;
		}

		static void SaveStrings (BinaryWriter writer, string[] array)
		{
			if (array == null) {
				writer.Write (0);
				return;
			}

			writer.Write (array.Length);
			for (int i = 0; i < array.Length; i++)
				writer.Write (array[i] ?? string.Empty);
		}

		static Uri LoadUri (BinaryReader reader)
		{
			var uri = reader.ReadString ();

			if (string.IsNullOrEmpty (uri))
				return null;

			return new Uri (uri);
		}

		static void SaveUri (BinaryWriter writer, Uri uri)
		{
			if (uri != null)
				writer.Write (uri.ToString ());
			else
				writer.Write (string.Empty);
		}

		static void LoadBodyPartBasic (BinaryReader reader, BodyPartBasic basic)
		{
			basic.PartSpecifier = reader.ReadString ();
			basic.ContentType = ContentType.Parse (reader.ReadString ());
			basic.ContentDisposition = ContentDisposition.Parse (reader.ReadString ());
			basic.ContentTransferEncoding = reader.ReadString ();
			basic.ContentDescription = reader.ReadString ();
			basic.ContentId = reader.ReadString ();
			basic.ContentMd5 = reader.ReadString ();
			basic.ContentLanguage = LoadStrings (reader);
			basic.ContentLocation = LoadUri (reader);
			basic.Octets = reader.ReadUInt32 ();
		}

		static void SaveBodyPartBasic (BinaryWriter writer, BodyPartBasic basic)
		{
			writer.Write (basic.PartSpecifier);
			writer.Write (basic.ContentType.ToString ());
			writer.Write (basic.ContentDisposition.ToString ());
			writer.Write (basic.ContentTransferEncoding);
			writer.Write (basic.ContentDescription);
			writer.Write (basic.ContentId);
			writer.Write (basic.ContentMd5);
			SaveStrings (writer, basic.ContentLanguage);
			SaveUri (writer, basic.ContentLocation);
			writer.Write (basic.Octets);
		}

		static void LoadBodyPartText (BinaryReader reader, BodyPartText text)
		{
			LoadBodyPartBasic (reader, text);
			text.Lines = reader.ReadUInt32 ();
		}

		static void SaveBodyPartText (BinaryWriter writer, BodyPartText text)
		{
			SaveBodyPartBasic (writer, text);
			writer.Write (text.Lines);
		}

		static void LoadBodyPartMessage (BinaryReader reader, BodyPartMessage rfc822)
		{
			LoadBodyPartBasic (reader, rfc822);
			rfc822.Envelope = LoadEnvelope (reader);
			rfc822.Body = LoadBodyStructure (reader);
			rfc822.Lines = reader.ReadUInt32 ();
		}

		static void SaveBodyPartMessage (BinaryWriter writer, BodyPartMessage rfc822)
		{
			SaveBodyPartBasic (writer, rfc822);
			SaveEnvelope (writer, rfc822.Envelope);
			SaveBodyStructure (writer, rfc822.Body);
			writer.Write (rfc822.Lines);
		}

		static void LoadBodyPartMultipart (BinaryReader reader, BodyPartMultipart multipart)
		{
			multipart.PartSpecifier = reader.ReadString ();
			multipart.ContentType = ContentType.Parse (reader.ReadString ());
			multipart.ContentDisposition = ContentDisposition.Parse (reader.ReadString ());
			multipart.ContentLanguage = LoadStrings (reader);
			multipart.ContentLocation = LoadUri (reader);

			int n = reader.ReadInt32 ();
			for (int i = 0; i < n; i++)
				multipart.BodyParts.Add (LoadBodyStructure (reader));
		}

		static void SaveBodyPartMultipart (BinaryWriter writer, BodyPartMultipart multipart)
		{
			writer.Write (multipart.PartSpecifier);
			writer.Write (multipart.ContentType.ToString ());
			writer.Write (multipart.ContentDisposition.ToString ());
			SaveStrings (writer, multipart.ContentLanguage);
			SaveUri (writer, multipart.ContentLocation);

			writer.Write (multipart.BodyParts.Count);
			foreach (var part in multipart.BodyParts)
				SaveBodyStructure (writer, part);
		}

		enum BodyPartType
		{
			Basic,
			Text,
			Message,
			Multipart
		}

		static BodyPart LoadBodyStructure (BinaryReader reader)
		{
			var type = (BodyPartType) reader.ReadInt32 ();

			switch (type) {
			case BodyPartType.Basic:
				var basic = new BodyPartBasic ();
				LoadBodyPartBasic (reader, basic);
				return basic;
			case BodyPartType.Text:
				var text = new BodyPartText ();
				LoadBodyPartText (reader, text);
				return text;
			case BodyPartType.Message:
				var rfc822 = new BodyPartMessage ();
				LoadBodyPartMessage (reader, rfc822);
				return rfc822;
			case BodyPartType.Multipart:
				var multipart = new BodyPartMultipart ();
				LoadBodyPartMultipart (reader, multipart);
				return multipart;
			default:
				throw new NotSupportedException ();
			}
		}

		static void SaveBodyStructure (BinaryWriter writer, BodyPart body)
		{
			if (body is BodyPartText text) {
				writer.Write ((int) BodyPartType.Text);
				SaveBodyPartText (writer, text);
			} else if (body is BodyPartMessage rfc822) {
				writer.Write ((int) BodyPartType.Message);
				SaveBodyPartMessage (writer, rfc822);
			} else if (body is BodyPartMultipart multipart) {
				writer.Write ((int) BodyPartType.Multipart);
				SaveBodyPartMultipart (writer, multipart);
			} else {
				var basic = (BodyPartBasic) body;
				writer.Write ((int) BodyPartType.Basic);
				SaveBodyPartBasic (writer, basic);
			}
		}

		static List<Annotation> LoadAnnotations (BinaryReader reader)
		{
			var annotations = new List<Annotation> ();
			var n = reader.ReadInt32 ();

			for (int i = 0; i < n; i++) {
				var path = reader.ReadString ();
				var entry = AnnotationEntry.Parse (path);
				var annotation = new Annotation (entry);

				annotations.Add (annotation);

				var nattrs = reader.ReadInt32 ();
				for (int j = 0; j < nattrs; j++) {
					var specifier = reader.ReadString ();
					var value = reader.ReadString ();

					var attribute = new AnnotationAttribute (specifier);
					annotation.Properties[attribute] = value;
				}
			}

			return annotations;
		}

		static void SaveAnnotations (BinaryWriter writer, IReadOnlyList<Annotation> annotations)
		{
			writer.Write (annotations.Count);

			foreach (var annotation in annotations) {
				writer.Write (annotation.Entry.Entry);
				writer.Write (annotation.Properties.Count);

				foreach (var attribute in annotation.Properties) {
					writer.Write (attribute.Key.Specifier);
					writer.Write (attribute.Value);
				}
			}
		}

		static MessageSummary LoadMessageSummary (BinaryReader reader, ref byte[] buffer, uint uidValidity, int index, CancellationToken cancellationToken)
		{
			cancellationToken.ThrowIfCancellationRequested ();

			var summary = new MessageSummary (index);
			var fields = (MessageSummaryItems) reader.ReadInt32 ();
			string references = null;

			summary.Fields = fields;

			if ((fields & MessageSummaryItems.UniqueId) != 0)
				summary.UniqueId = new UniqueId (uidValidity, reader.ReadUInt32 ());

			if ((fields & MessageSummaryItems.ModSeq) != 0)
				summary.ModSeq = reader.ReadUInt64 ();

			if ((fields & MessageSummaryItems.Flags) != 0) {
				summary.Flags = (MessageFlags) reader.ReadUInt32 ();

				int n = reader.ReadInt32 ();
				for (int i = 0; i < n; i++)
					((HashSet<string>) summary.Keywords).Add (reader.ReadString ());
			}

			if ((fields & MessageSummaryItems.InternalDate) != 0)
				summary.InternalDate = LoadDateTimeOffset (reader);

			if ((fields & MessageSummaryItems.Size) != 0)
				summary.Size = reader.ReadUInt32 ();

			if ((fields & MessageSummaryItems.Envelope) != 0)
				summary.Envelope = LoadEnvelope (reader);

			if ((fields & MessageSummaryItems.Headers) != 0) {
				int n = reader.ReadInt32 ();

				if (buffer.Length < n)
					Array.Resize (ref buffer, n);

				reader.Read (buffer, 0, n);

				using (var stream = new MemoryStream (buffer, 0, n, false))
					summary.Headers = HeaderList.Load (stream, cancellationToken);

				if ((fields & MessageSummaryItems.References) != 0) {
					references = summary.Headers[HeaderId.References];
					summary.References = new MessageIdList ();
				}
			} else if ((fields & MessageSummaryItems.References) != 0) {
				summary.References = new MessageIdList ();
				references = reader.ReadString ();
			}

			if (references != null) {
				foreach (var msgid in MimeUtils.EnumerateReferences (references))
					summary.References.Add (msgid);
			}

			if ((fields & (MessageSummaryItems.Body | MessageSummaryItems.BodyStructure)) != 0)
				summary.Body = LoadBodyStructure (reader);

			if ((fields & MessageSummaryItems.PreviewText) != 0)
				summary.PreviewText = reader.ReadString ();

			// GMail fields
			if ((fields & MessageSummaryItems.GMailLabels) != 0) {
				int n = reader.ReadInt32 ();

				for (int i = 0; i < n; i++)
					summary.GMailLabels.Add (reader.ReadString ());
			}

			if ((fields & MessageSummaryItems.GMailMessageId) != 0)
				summary.GMailMessageId = reader.ReadUInt64 ();

			if ((fields & MessageSummaryItems.GMailThreadId) != 0)
				summary.GMailThreadId = reader.ReadUInt64 ();

			// Uncommon fields
			if ((fields & MessageSummaryItems.Annotations) != 0)
				summary.Annotations = LoadAnnotations (reader);

			if ((fields & MessageSummaryItems.EmailId) != 0)
				summary.EmailId = reader.ReadString ();

			if ((fields & MessageSummaryItems.ThreadId) != 0)
				summary.ThreadId = reader.ReadString ();

			if ((fields & MessageSummaryItems.SaveDate) != 0)
				summary.SaveDate = LoadDateTimeOffset (reader);

			return summary;
		}

		static void SaveMessageSummary (BinaryWriter writer, MemoryStream memory, IMessageSummary summary, CancellationToken cancellationToken)
		{
			cancellationToken.ThrowIfCancellationRequested ();

			writer.Write ((int) summary.Fields);

			if ((summary.Fields & MessageSummaryItems.UniqueId) != 0)
				writer.Write (summary.UniqueId.Id);

			if ((summary.Fields & MessageSummaryItems.ModSeq) != 0)
				writer.Write (summary.ModSeq ?? 0);

			if ((summary.Fields & MessageSummaryItems.Flags) != 0) {
				writer.Write ((uint) summary.Flags);

				writer.Write (summary.Keywords.Count);
				foreach (var keyword in summary.Keywords)
					writer.Write (keyword);
			}

			if ((summary.Fields & MessageSummaryItems.InternalDate) != 0)
				SaveDateTimeOffset (writer, summary.InternalDate);

			if ((summary.Fields & MessageSummaryItems.Size) != 0)
				writer.Write (summary.Size ?? 0);

			if ((summary.Fields & MessageSummaryItems.Envelope) != 0)
				SaveEnvelope (writer, summary.Envelope);

			if ((summary.Fields & MessageSummaryItems.Headers) != 0) {
				memory.Position = 0;
				summary.Headers.WriteTo (formatOptions, memory);

				var buffer = memory.GetBuffer ();
				int length = (int) memory.Position;

				writer.Write (length);
				writer.Write (buffer, 0, length);
			} else if ((summary.Fields & MessageSummaryItems.References) != 0) {
				writer.Write (summary.References.ToString ());
			}

			if ((summary.Fields & (MessageSummaryItems.Body | MessageSummaryItems.BodyStructure)) != 0)
				SaveBodyStructure (writer, summary.Body);

			if ((summary.Fields & MessageSummaryItems.PreviewText) != 0)
				writer.Write (summary.PreviewText);

			// GMail fields
			if ((summary.Fields & MessageSummaryItems.GMailLabels) != 0) {
				writer.Write (summary.GMailLabels.Count);

				foreach (var label in summary.GMailLabels)
					writer.Write (label);
			}

			if ((summary.Fields & MessageSummaryItems.GMailMessageId) != 0)
				writer.Write (summary.GMailMessageId ?? 0);

			if ((summary.Fields & MessageSummaryItems.GMailThreadId) != 0)
				writer.Write (summary.GMailThreadId ?? 0);

			// Uncommon fields
			if ((summary.Fields & MessageSummaryItems.Annotations) != 0)
				SaveAnnotations (writer, summary.Annotations);

			if ((summary.Fields & MessageSummaryItems.EmailId) != 0)
				writer.Write (summary.EmailId);

			if ((summary.Fields & MessageSummaryItems.ThreadId) != 0)
				writer.Write (summary.ThreadId);

			if ((summary.Fields & MessageSummaryItems.SaveDate) != 0)
				SaveDateTimeOffset (writer, summary.SaveDate);
		}

		public void Load (CancellationToken cancellationToken = default)
		{
			cancellationToken.ThrowIfCancellationRequested ();

			var path = Path.Combine (cacheDir, IndexFileName);

			using (var stream = File.OpenRead (path)) {
				using (var reader = new BinaryReader (stream, Encoding.UTF8, true)) {
					var buffer = new byte[4096];

					header = new IndexHeader ();

					try {
						header.Version = reader.ReadInt32 ();

						if (header.Version != ExpectedVersion)
							throw new FormatException ("Unexpected database version.");

						header.UidNext = reader.ReadUInt32 ();
						header.UidValidity = reader.ReadUInt32 ();
						header.HighestModSeq = reader.ReadUInt64 ();

						cancellationToken.ThrowIfCancellationRequested ();

						int count = reader.ReadInt32 ();
						messages = new List<MessageSummary> (count);
						for (int i = 0; i < count; i++) {
							var message = LoadMessageSummary (reader, ref buffer, header.UidValidity, i, cancellationToken);
							messages.Add (message);
						}
					} catch (Exception) {
						messages = null;
						header = null;
						throw;
					}
				}
			}
		}

		public void Save (CancellationToken cancellationToken = default)
		{
			cancellationToken.ThrowIfCancellationRequested ();

			bool dirExists = Directory.Exists (cacheDir);

			if (!dirExists)
				Directory.CreateDirectory (cacheDir);

			var temp = Path.Combine (cacheDir, TempIndexFileName);

			try {
				using (var stream = File.Create (temp, 4096)) {
					using (var writer = new BinaryWriter (stream, Encoding.UTF8, true)) {
						writer.Write (ExpectedVersion);

						writer.Write (header.UidNext);
						writer.Write (header.UidValidity);
						writer.Write (header.HighestModSeq);

						writer.Write (messages.Count);

						using (var memory = new MemoryStream ()) {
							for (int i = 0; i < messages.Count; i++)
								SaveMessageSummary (writer, memory, messages[i], cancellationToken);
						}
					}
				}

				var path = Path.Combine (cacheDir, IndexFileName);

				if (File.Exists (path))
					File.Replace (temp, path, null);
				else
					File.Move (temp, path);
			} catch (Exception) {
				if (File.Exists (temp))
					File.Delete (temp);

				if (!dirExists)
					Directory.Delete (cacheDir);

				throw;
			}
		}

		public void OnUidValidityChanged (uint uidValidity)
		{
			if (header is null || header.UidValidity == uidValidity)
				return;

			header.UidValidity = uidValidity;
			messages.Clear ();
			IsDirty = true;

			// Delete the summary index file if it exists because it is no longer valid.
			var path = Path.Combine (cacheDir, IndexFileName);

			if (File.Exists (path))
				File.Delete (path);
		}

		public void OnUidNextChanged (uint nextUid)
		{
			if (header is null || header.UidNext == nextUid)
				return;

			header.UidNext = nextUid;
			IsDirty = true;
		}

		public void OnHighestModSeqChanged (ulong highestModSeq)
		{
			if (header is null || header.HighestModSeq == highestModSeq)
				return;

			header.HighestModSeq = highestModSeq;
			IsDirty = true;
		}

		public void Rename (string newFullName)
		{
			if (newFullName == null)
				throw new ArgumentNullException (nameof (newFullName));

			var newCacheDir = Path.Combine (baseCacheDir, EncodeFolderName (newFullName));

			if (Directory.Exists (cacheDir))
				Directory.Move (cacheDir, newCacheDir);

			cacheDir = newCacheDir;
		}

		public void Delete ()
		{
			header = null;
			messages = null;
			IsDirty = false;

			Directory.Delete (cacheDir, true);
		}

		void Update (int index, IMessageSummary updated)
		{
			var message = messages[index];

			if (message.UniqueId.IsValid && updated.UniqueId.IsValid)
				Debug.Assert (message.UniqueId.Id == updated.UniqueId.Id);

			message.Fields |= updated.Fields;

			if ((updated.Fields & MessageSummaryItems.UniqueId) != 0)
				message.UniqueId = updated.UniqueId;

			if ((updated.Fields & MessageSummaryItems.ModSeq) != 0)
				message.ModSeq = updated.ModSeq;

			if ((updated.Fields & MessageSummaryItems.Flags) != 0) {
				message.Fields |= MessageSummaryItems.Flags;
				message.Flags = updated.Flags;

				var keywords = (HashSet<string>) message.Keywords;

				keywords.Clear ();
				foreach (var keyword in updated.Keywords)
					keywords.Add (keyword);
			}

			if ((updated.Fields & MessageSummaryItems.InternalDate) != 0)
				message.InternalDate = updated.InternalDate;

			if ((updated.Fields & MessageSummaryItems.Size) != 0)
				message.Size = updated.Size;

			if ((updated.Fields & MessageSummaryItems.Envelope) != 0)
				message.Envelope = updated.Envelope;

			if ((updated.Fields & MessageSummaryItems.Headers) != 0)
				message.Headers = updated.Headers;

			if ((updated.Fields & MessageSummaryItems.References) != 0)
				message.References = updated.References;

			if ((updated.Fields & (MessageSummaryItems.Body | MessageSummaryItems.BodyStructure)) != 0)
				message.Body = updated.Body;

			if ((updated.Fields & MessageSummaryItems.PreviewText) != 0)
				message.PreviewText = updated.PreviewText;

			// GMail fields
			if ((updated.Fields & MessageSummaryItems.GMailLabels) != 0) {
				message.GMailLabels.Clear ();
				foreach (var label in updated.GMailLabels)
					message.GMailLabels.Add (label);
			}

			if ((updated.Fields & MessageSummaryItems.GMailMessageId) != 0)
				message.GMailMessageId = updated.GMailMessageId;

			if ((updated.Fields & MessageSummaryItems.GMailThreadId) != 0)
				message.GMailThreadId = updated.GMailThreadId;

			// Uncommon fields
			if ((updated.Fields & MessageSummaryItems.Annotations) != 0)
				message.Annotations = updated.Annotations;

			if ((updated.Fields & MessageSummaryItems.EmailId) != 0)
				message.EmailId = updated.EmailId;

			if ((updated.Fields & MessageSummaryItems.ThreadId) != 0)
				message.ThreadId = updated.ThreadId;

			if ((updated.Fields & MessageSummaryItems.SaveDate) != 0)
				message.SaveDate = updated.SaveDate;
		}

		public void AddOrUpdate (IMessageSummary message)
		{
			int index = message.Index;

			if (index >= messages.Count) {
				// backfill any missing message summaries that we haven't received yet
#if NET6_0_OR_GREATER
				messages.EnsureCapacity (index + 1);
#endif
				for (int i = messages.Count; i < index; i++)
					messages.Add (new MessageSummary (i));

				messages.Add (new MessageSummary (index));
			}

			Update (index, message);
			IsDirty = true;
		}

		public void Expunge (int index)
		{
			if (index < 0)
				throw new ArgumentOutOfRangeException (nameof (index));

			if (messages == null)
				throw new InvalidOperationException ();

			if (index >= messages.Count)
				return;

			messages.RemoveAt (index);
			IsDirty = true;
		}

		static SortOrder GetSortOrder (IEnumerable<UniqueId> uids)
		{
			if (uids is UniqueIdSet @set)
				return @set.SortOrder;

			if (uids is UniqueIdRange range)
				return range.Start.Id <= range.End.Id ? SortOrder.Ascending : SortOrder.Descending;

			return SortOrder.None;
		}

		IEnumerable<int> GetAscendingIndexes (IEnumerable<UniqueId> uids)
		{
			int index = 0;

			foreach (var uid in uids) {
				while (index < messages.Count) {
					if (messages[index].UniqueId.IsValid) {
						if (messages[index].UniqueId.Id == uid.Id) {
							yield return index;
							break;
						} else if (messages[index].UniqueId.Id > uid.Id) {
							break;
						}
					}

					index++;
				}

				if (index >= messages.Count)
					break;
			}
		}

		IEnumerable<int> GetDescendingIndexes (IEnumerable<UniqueId> uids)
		{
			int index = messages.Count - 1;

			foreach (var uid in uids) {
				while (index >= 0) {
					if (messages[index].UniqueId.IsValid) {
						if (messages[index].UniqueId.Id == uid.Id) {
							yield return index;
							break;
						} else if (messages[index].UniqueId.Id < uid.Id) {
							break;
						}
					}

					index--;
				}

				if (index < 0)
					break;
			}
		}

		IEnumerable<int> GetUnorderedIndexes (IEnumerable<UniqueId> uids)
		{
			var map = new Dictionary<uint, int> ();
			int index = 0;

			foreach (var uid in uids) {
				if (map.TryGetValue (uid.Id, out var idx)) {
					yield return idx;
					continue;
				}

				while (index < messages.Count) {
					if (messages[index].UniqueId.IsValid) {
						map.Add (messages[index].UniqueId.Id, index);

						if (messages[index].UniqueId.Id == uid.Id)
							yield return index;
					}

					index++;
				}
			}
		}

		IEnumerable<int> GetIndexes (IEnumerable<UniqueId> uids, out SortOrder order)
		{
			order = GetSortOrder (uids);

			switch (order) {
			case SortOrder.Ascending: return GetAscendingIndexes (uids);
			case SortOrder.Descending: return GetDescendingIndexes (uids);
			default: return GetUnorderedIndexes (uids);
			}
		}

		public void Expunge (IEnumerable<UniqueId> uids)
		{
			if (uids == null)
				throw new ArgumentNullException (nameof (uids));

			if (messages == null)
				throw new InvalidOperationException ();

			if (messages.Count == 0)
				return;

			var indexes = GetIndexes (uids, out var order).ToArray ();

			// Note: We always want to remove indexes in descending order to avoid shifting indexes.
			switch (order) {
			case SortOrder.Ascending:
				for (int i = indexes.Length - 1; i >= 0; i--)
					messages.RemoveAt (indexes[i]);
				break;
			case SortOrder.Descending:
				for (int i = 0; i < indexes.Length; i++)
					messages.RemoveAt (indexes[i]);
				break;
			default:
				Array.Sort (indexes);
				goto case SortOrder.Ascending;
			}
		}

		public IList<IMessageSummary> Fetch (int min, int max, IFetchRequest request)
		{
			if (min < 0)
				throw new ArgumentOutOfRangeException (nameof (min));

			if (max != -1 && max < min)
				throw new ArgumentOutOfRangeException (nameof (max));

			if (request == null)
				throw new ArgumentNullException (nameof (request));

			if (messages == null)
				throw new InvalidOperationException ();

			if (max == -1)
				max = messages.Count - 1;

			var summaries = new List<IMessageSummary> ((max - min) + 1);
			var changedSince = request.ChangedSince;

			for (int index = min; index <= max; index++) {
				if (!changedSince.HasValue || (messages[index].ModSeq.HasValue && messages[index].ModSeq > changedSince.Value))
					summaries.Add (messages[index]);
			}

			return summaries;
		}

		public IList<IMessageSummary> Fetch (IList<int> indexes, IFetchRequest request)
		{
			if (indexes == null)
				throw new ArgumentNullException (nameof (indexes));

			if (request == null)
				throw new ArgumentNullException (nameof (request));

			if (messages == null)
				throw new InvalidOperationException ();

			if (indexes.Count == 0)
				return Array.Empty<IMessageSummary> ();

			var summaries = new List<IMessageSummary> (indexes.Count);
			var changedSince = request.ChangedSince;

			foreach (int index in indexes) {
				if (index < 0 || index >= messages.Count)
					throw new ArgumentOutOfRangeException (nameof (indexes));

				if (!changedSince.HasValue || (messages[index].ModSeq.HasValue && messages[index].ModSeq > changedSince.Value))
					summaries.Add (messages[index]);
			}

			return summaries;
		}

		public IList<IMessageSummary> Fetch (IList<UniqueId> uids, IFetchRequest request)
		{
			if (uids == null)
				throw new ArgumentNullException (nameof (uids));

			if (request == null)
				throw new ArgumentNullException (nameof (request));

			if (messages == null)
				throw new InvalidOperationException ();

			if (uids.Count == 0)
				return Array.Empty<IMessageSummary> ();

			var summaries = new List<IMessageSummary> (uids.Count);
			var changedSince = request.ChangedSince;

			foreach (var index in GetIndexes (uids, out _)) {
				if (!changedSince.HasValue || (messages[index].ModSeq.HasValue && messages[index].ModSeq > changedSince.Value))
					summaries.Add (messages[index]);
			}

			return summaries;
		}

		public void Dispose ()
		{
		}
	}
}
