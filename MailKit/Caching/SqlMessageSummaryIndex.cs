//
// SqlMessageSummaryIndex.cs
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
using System.Text;
using System.Data;
using System.Threading;
using System.Data.Common;
using System.Collections.Generic;

using MimeKit;

using MailKit.Search;

#if NET5_0_OR_GREATER
using IReadOnlySetOfStrings = System.Collections.Generic.IReadOnlySet<string>;
#else
using IReadOnlySetOfStrings = System.Collections.Generic.ISet<string>;
#endif

namespace MailKit.Caching {
	public class SqlMessageSummaryIndex : IMessageSummaryIndex
	{
		static readonly DateTime InvalidDateTime = new DateTime (0, DateTimeKind.Utc);

		static readonly DataTable[] DataTables;
		static readonly DataTable MessageTable;
		static readonly DataTable KeywordsTable;
		static readonly DataTable GMailLabelsTable;
		//static readonly DataTable AnnotationsTable;
		static readonly DataTable StatusTable;

		static SqlMessageSummaryIndex ()
		{
			MessageTable = CreateMessageTable ();
			KeywordsTable = CreateKeywordsTable ();
			GMailLabelsTable = CreateGMailLabelsTable ();
			//AnnotationsTable = CreateAnnotationsTable ();
			StatusTable = CreateStatusTable ();

			DataTables = new DataTable[] {
				StatusTable, MessageTable, KeywordsTable, GMailLabelsTable /*, AnnotationsTable */
			};
		}

		static DataTable CreateMessageTable ()
		{
			var table = new DataTable ("MESSAGES");
			table.Columns.Add (new DataColumn ("UID", typeof (long)) { AllowDBNull = false, Unique = true });
			table.Columns.Add (new DataColumn ("FETCHED", typeof (int)) { AllowDBNull = false });
			table.Columns.Add (new DataColumn ("INTERNALDATE", typeof (DateTime)) { AllowDBNull = true });
			table.Columns.Add (new DataColumn ("INTERNALTIMEZONE", typeof (long)) { AllowDBNull = true });
			table.Columns.Add (new DataColumn ("SIZE", typeof (long)) { AllowDBNull = true });
			table.Columns.Add (new DataColumn ("FLAGS", typeof (int)) { AllowDBNull = true });
			table.Columns.Add (new DataColumn ("MODSEQ", typeof (long)) { AllowDBNull = true });

			// ENVELOPE
			table.Columns.Add (new DataColumn ("DATE", typeof (DateTime)) { AllowDBNull = true });
			table.Columns.Add (new DataColumn ("TIMEZONE", typeof (long)) { AllowDBNull = true });
			table.Columns.Add (new DataColumn ("SUBJECT", typeof (string)) { AllowDBNull = true });
			table.Columns.Add (new DataColumn ("FROM", typeof (string)) { AllowDBNull = true });
			table.Columns.Add (new DataColumn ("SENDER", typeof (string)) { AllowDBNull = true });
			table.Columns.Add (new DataColumn ("REPLYTO", typeof (string)) { AllowDBNull = true });
			table.Columns.Add (new DataColumn ("TO", typeof (string)) { AllowDBNull = true });
			table.Columns.Add (new DataColumn ("CC", typeof (string)) { AllowDBNull = true });
			table.Columns.Add (new DataColumn ("BCC", typeof (string)) { AllowDBNull = true });
			table.Columns.Add (new DataColumn ("INREPLYTO", typeof (string)) { AllowDBNull = true });
			table.Columns.Add (new DataColumn ("MESSAGEID", typeof (string)) { AllowDBNull = true });

			// REFERENCES
			table.Columns.Add (new DataColumn ("REFERENCES", typeof (string)) { AllowDBNull = true });

			// BODYSTRUCTURE
			table.Columns.Add (new DataColumn ("BODYSTRUCTURE", typeof (string)) { AllowDBNull = true });

			// PREVIEWTEXT
			table.Columns.Add (new DataColumn ("PREVIEWTEXT", typeof (string)) { AllowDBNull = true });

			// GMail-specific features
			table.Columns.Add (new DataColumn ("XGMMSGID", typeof (long)) { AllowDBNull = true });
			table.Columns.Add (new DataColumn ("XGMTHRID", typeof (long)) { AllowDBNull = true });

			// OBJECTID extension
			table.Columns.Add (new DataColumn ("EMAILID", typeof (string)) { AllowDBNull = true });
			table.Columns.Add (new DataColumn ("THREADID", typeof (string)) { AllowDBNull = true });

			// SAVEDATE extension
			//table.Columns.Add(new DataColumn("SAVEDATE", typeof (DateTime)) { AllowDBNull = true });
			//table.Columns.Add(new DataColumn("SAVEDATETIMEZONE", typeof (long)) { AllowDBNull = true });

			// Set the UID as the primary key
			table.PrimaryKey = new DataColumn[] { table.Columns[0] };

			return table;
		}

		static DataTable CreateKeywordsTable ()
		{
			var table = new DataTable ("KEYWORDS");
			table.Columns.Add (new DataColumn ("ROWID", typeof (int)) { AutoIncrement = true });
			table.Columns.Add (new DataColumn ("UID", typeof (long)) { AllowDBNull = false });
			table.Columns.Add (new DataColumn ("KEYWORD", typeof (string)) { AllowDBNull = false });
			table.PrimaryKey = new DataColumn[] { table.Columns[0] };

			return table;
		}

		static DataTable CreateGMailLabelsTable ()
		{
			var table = new DataTable ("XGMLABELS");
			table.Columns.Add (new DataColumn ("ROWID", typeof (int)) { AutoIncrement = true });
			table.Columns.Add (new DataColumn ("UID", typeof (long)) { AllowDBNull = false });
			table.Columns.Add (new DataColumn ("KEYWORD", typeof (string)) { AllowDBNull = false });
			table.PrimaryKey = new DataColumn[] { table.Columns[0] };

			return table;
		}

		static DataTable CreateStatusTable ()
		{
			var table = new DataTable ("STATUS");
			table.Columns.Add (new DataColumn ("ROWID", typeof (int)) { AllowDBNull = false, Unique = true });
			table.Columns.Add (new DataColumn ("UIDVALIDITY", typeof (long)) { AllowDBNull = false });
			table.Columns.Add (new DataColumn ("UIDNEXT", typeof (long)) { AllowDBNull = true });
			table.Columns.Add (new DataColumn ("HIGHESTMODSEQ", typeof (long)) { AllowDBNull = true });

			//table.Columns.Add (new DataColumn ("COUNT", typeof (long)) { AllowDBNull = false });
			//table.Columns.Add (new DataColumn ("RECENT", typeof (long)) { AllowDBNull = false });
			//table.Columns.Add (new DataColumn ("UNREAD", typeof (long)) { AllowDBNull = false });
			//table.Columns.Add (new DataColumn ("SIZE", typeof (long)) { AllowDBNull = false });

			//table.Columns.Add (new DataColumn ("APPENDLIMIT", typeof (long)) { AllowDBNull = true });
			//table.Columns.Add (new DataColumn ("MAILBOXID", typeof (string)) { AllowDBNull = true });

			table.PrimaryKey = new DataColumn[] { table.Columns[0] };

			return table;
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

		const string IndexFileName = "index.sqlite";
		string baseCacheDir, cacheDir;
		SQLiteConnection sqlite;

		public SqlMessageSummaryIndex (string baseCacheDir, string folderFullName)
		{
			this.baseCacheDir = baseCacheDir;
			this.cacheDir = Path.Combine (baseCacheDir, EncodeFolderName (folderFullName));
		}

		public ulong? HighestModSeq {
			get; private set;
		}

		public uint? UidNext {
			get; private set;
		}

		public uint? UidValidity {
			get; private set;
		}

		public int Count {
			get; private set;
		}

		public bool IsDirty {
			get; private set;
		}

		void Open ()
		{
			if (!Directory.Exists (cacheDir))
				Directory.CreateDirectory (cacheDir);

			var path = Path.Combine (cacheDir, IndexFileName);
			var builder = new SQLiteConnectionStringBuilder {
				DateTimeFormat = SQLiteDateFormats.ISO8601,
				DataSource = path
			};

			sqlite = new SQLiteConnection (builder.ConnectionString);

			sqlite.Open ();
		}

		void Close ()
		{
			if (sqlite != null) {
				sqlite.Close ();
				sqlite.Dispose ();
				sqlite = null;
			}
		}

		DbCommand CreateLoadStatusCommand ()
		{
			var command = sqlite.CreateCommand ();
			command.CommandText = $"SELECT * FROM {StatusTable.TableName} WHERE ROWID = @ROWID LIMIT 1";
			command.Parameters.AddWithValue ("@ROWID", 0);
			command.CommandType = CommandType.Text;
			return command;
		}

		void ReadStatus (DbDataReader reader)
		{
			for (int i = 0; i < reader.FieldCount; i++) {
				switch (reader.GetName (i)) {
				case "UIDVALIDITY":
					UidValidity = (uint) reader.GetInt64 (i);
					break;
				case "UIDNEXT":
					if (!reader.IsDBNull (i))
						UidNext = (uint) reader.GetInt64 (i);
					else
						UidNext = null;
					break;
				case "HIGHESTMODSEQ":
					if (!reader.IsDBNull (i))
						HighestModSeq = (ulong) reader.GetInt64 (i);
					else
						HighestModSeq = null;
					break;
				}
			}
		}

		bool LoadStatus ()
		{
			using (var command = CreateLoadStatusCommand ()) {
				using (var reader = command.ExecuteReader ()) {
					if (!reader.Read ())
						return false;

					ReadStatus (reader);

					return true;
				}
			}
		}

		public void Load (CancellationToken cancellationToken = default)
		{
			cancellationToken.ThrowIfCancellationRequested ();

			if (sqlite != null)
				return;

			Open ();

			foreach (var dataTable in DataTables)
				sqlite.CreateTable (dataTable);

			if (LoadStatus ())
				return;

			SaveStatus ();
		}

		DbCommand CreateSaveStatusCommand ()
		{
			var command = sqlite.CreateCommand ();
			command.Parameters.AddWithValue ("@ROWID", 0);
			command.Parameters.AddWithValue ("@UIDVALIDITY", (long) UidValidity);
			command.Parameters.AddWithValue ("@UIDNEXT", UidNext.HasValue ? (object) UidNext.Value : null);
			command.Parameters.AddWithValue ("@HIGHESTMODSEQ", HighestModSeq.HasValue ? (object) HighestModSeq.Value : null);

			command.CommandText = $"INSERT OR REPLACE INTO {StatusTable.TableName} (ROWID, UIDVALIDITY, UIDNEXT, HIGHESTMODSEQ) VALUES(@ROWID, @UIDVALIDITY, @UIDNEXT, @HIGHESTMODSEQ)";
			command.CommandType = CommandType.Text;

			return command;
		}

		void SaveStatus ()
		{
			using (var command = CreateSaveStatusCommand ())
				command.ExecuteNonQuery ();
		}

		public void Save (CancellationToken cancellationToken = default)
		{
			cancellationToken.ThrowIfCancellationRequested ();

			bool dirExists = Directory.Exists (cacheDir);

			if (!dirExists)
				Directory.CreateDirectory (cacheDir);

			SaveStatus ();
		}

		void DropTable (string tableName)
		{
			using (var command = sqlite.CreateCommand ()) {
				command.CommandText = $"DROP TABLE IF EXISTS {tableName}";
				command.CommandType = CommandType.Text;

				command.ExecuteNonQuery ();
			}
		}

		void Clear ()
		{
			// TODO: clear message files as well (once that gets implemented)
			using (var transaction = sqlite.BeginTransaction ()) {
				DropTable (MessageTable.TableName);
				DropTable (KeywordsTable.TableName);
				DropTable (GMailLabelsTable.TableName);

				sqlite.CreateTable (MessageTable);
				sqlite.CreateTable (KeywordsTable);
				sqlite.CreateTable (GMailLabelsTable);

				transaction.Commit ();
			}
		}

		public void OnUidValidityChanged (uint uidValidity)
		{
			if (UidValidity == uidValidity)
				return;

			Clear ();

			UidValidity = uidValidity;
			SaveStatus ();
		}

		public void OnUidNextChanged (uint nextUid)
		{
			if (UidNext == nextUid)
				return;

			UidNext = nextUid;
			SaveStatus ();
		}

		public void OnHighestModSeqChanged (ulong highestModSeq)
		{
			if (HighestModSeq == highestModSeq)
				return;

			HighestModSeq = highestModSeq;
			SaveStatus ();
		}

		public void Rename (string newFullName)
		{
			if (newFullName == null)
				throw new ArgumentNullException (nameof (newFullName));

			var reopen = sqlite != null;

			Close ();

			var newCacheDir = Path.Combine (baseCacheDir, EncodeFolderName (newFullName));

			if (Directory.Exists (cacheDir))
				Directory.Move (cacheDir, newCacheDir);

			cacheDir = newCacheDir;

			if (reopen)
				Open ();
		}

		public void Delete ()
		{
			Close ();
			
			IsDirty = false;

			Directory.Delete (cacheDir, true);
		}

		bool TryGetUniqueId (int index, out UniqueId uid)
		{
			using (var command = sqlite.CreateCommand ()) {
				command.Parameters.AddWithValue ("@INDEX", (long) index);

				command.CommandText = $"SELECT UID FROM {MessageTable.TableName} ORDER BY UID LIMIT 1 OFFSET @INDEX";
				command.CommandType = CommandType.Text;

				using (var reader = command.ExecuteReader (CommandBehavior.SingleRow)) {
					if (reader.Read ()) {
						int column = reader.GetOrdinal ("UID");

						if (column != -1) {
							uid = new UniqueId ((uint) reader.GetInt64 (column));
							return true;
						}
					}

					uid = UniqueId.Invalid;

					return false;
				}
			}
		}

		public IList<UniqueId> GetAllCachedUids (CancellationToken cancellationToken = default)
		{
			using (var command = sqlite.CreateCommand ()) {
				command.CommandText = $"SELECT UID FROM {MessageTable.TableName}";
				command.CommandType = CommandType.Text;

				using (var reader = command.ExecuteReader ()) {
					var uids = new UniqueIdSet (SortOrder.Ascending);

					while (reader.Read ()) {
						int index = reader.GetOrdinal ("UID");
						var uid = (uint) reader.GetInt64 (index);

						uids.Add (new UniqueId (uid));
					}

					return uids;
				}
			}
		}

		/// <summary>
		/// Get a list of all of the UIDs in the cache where the summary information is incomplete.
		/// </summary>
		/// <param name="desiredItems">The summary information that is desired.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <returns>The list of all UIDs that do not have all of the desired summary information.</returns>
		public IList<UniqueId> GetIncompleteCachedUids (MessageSummaryItems desiredItems, CancellationToken cancellationToken = default)
		{
			using (var command = sqlite.CreateCommand ()) {
				command.CommandText = $"SELECT UID FROM {MessageTable.TableName} WHERE FETCHED & FIELDS != @FIELDS";
				command.Parameters.AddWithValue ("@FIELDS", (int) desiredItems);
				command.CommandType = CommandType.Text;

				using (var reader = command.ExecuteReader ()) {
					var uids = new UniqueIdSet (SortOrder.Ascending);

					while (reader.Read (cancellationToken)) {
						int index = reader.GetOrdinal ("UID");
						var uid = (uint) reader.GetInt64 (index);

						uids.Add (new UniqueId (uid));
					}

					return uids;
				}
			}
		}

		public void Insert (UniqueId uid)
		{
			using (var command = sqlite.CreateCommand ()) {
				command.CommandText = $"INSERT INTO {MessageTable.TableName} OR IGNORE (UID, FETCHED) VALUES(@UID, @FETCHED)";
				command.Parameters.AddWithValue ("@FETCHED", (int) MessageSummaryItems.UniqueId);
				command.Parameters.AddWithValue ("@UID", (long) uid.Id);
				command.CommandType = CommandType.Text;
				command.ExecuteNonQuery ();
			}
		}

		object GetValue (UniqueId uid, IMessageSummary message, string columnName)
		{
			switch (columnName) {
			case "UID":
				return (long) uid.Id;
			case "INTERNALDATE":
				return message.InternalDate?.ToUniversalTime ().DateTime;
			case "INTERNALTIMEZONE":
				return message.InternalDate?.Offset.Ticks;
			case "SIZE":
				if (message.Size.HasValue)
					return (long) message.Size.Value;
				return null;
			case "FLAGS":
				if (message.Flags.HasValue)
					return (long) message.Flags.Value;
				return null;
			case "MODSEQ":
				if (message.ModSeq.HasValue)
					return (long) message.ModSeq.Value;
				return null;
			case "DATE":
				return message.Envelope?.Date?.ToUniversalTime ().DateTime;
			case "TIMEZONE":
				return message.Envelope?.Date?.Offset.Ticks;
			case "SUBJECT":
				return message.Envelope?.Subject;
			case "FROM":
				return message.Envelope?.From.ToString ();
			case "SENDER":
				return message.Envelope?.Sender.ToString ();
			case "REPLYTO":
				return message.Envelope?.ReplyTo.ToString ();
			case "TO":
				return message.Envelope?.To.ToString ();
			case "CC":
				return message.Envelope?.Cc.ToString ();
			case "BCC":
				return message.Envelope?.Bcc.ToString ();
			case "INREPLYTO":
				return message.Envelope?.InReplyTo;
			case "MESSAGEID":
				return message.Envelope?.MessageId;
			case "REFERENCES":
				return message.References?.ToString ();
			case "BODYSTRUCTURE":
				return message.Body?.ToString ();
			case "PREVIEWTEXT":
				return message.PreviewText;
			case "XGMMSGID":
				if (message.GMailMessageId.HasValue)
					return (long) message.GMailMessageId.Value;
				return null;
			case "XGMTHRID":
				if (message.GMailThreadId.HasValue)
					return (long) message.GMailThreadId.Value;
				return null;
			case "EMAILID":
				return message.EmailId;
			case "THREADID":
				return message.ThreadId;
			//case "SAVEDATE":
			//	if (message.SaveDate.HasValue)
			//		return message.SaveDate.Value.ToUniversalTime().DateTime;
			//	return null;
			//case "SAVEDATETIMEZONE":
			//	if (message.SaveDate.HasValue)
			//		return message.SaveDate.Value.Offset.Ticks;
			//	return null;
			default:
				return null;
			}
		}

		void UpdateKeywords (UniqueId uid, IReadOnlySetOfStrings keywords)
		{
			var oldKeywords = new HashSet<string> (StringComparer.OrdinalIgnoreCase);

			LoadKeywords (uid, oldKeywords);

			using (var transaction = sqlite.BeginTransaction ()) {
				try {
					foreach (var keyword in oldKeywords) {
						if (keywords.Contains (keyword))
							continue;

						using (var command = sqlite.CreateCommand ()) {
							command.CommandText = $"DELETE FROM {KeywordsTable.TableName} WHERE UID = @UID AND KEYWORD = @KEYWORD";
							command.Parameters.AddWithValue ("@UID", (long) uid.Id);
							command.Parameters.AddWithValue ("@KEYWORD", keyword);
							command.CommandType = CommandType.Text;

							command.ExecuteNonQuery ();
						}
					}

					foreach (var keyword in keywords) {
						if (oldKeywords.Contains (keyword))
							continue;

						using (var command = sqlite.CreateCommand ()) {
							command.CommandText = $"INSERT INTO {KeywordsTable.TableName} (UID, KEYWORD) VALUES(@UID, @KEYWORD)";
							command.Parameters.AddWithValue ("@UID", (long) uid.Id);
							command.Parameters.AddWithValue ("@KEYWORD", keyword);
							command.CommandType = CommandType.Text;

							command.ExecuteNonQuery ();
						}
					}

					transaction.Commit ();
				} catch {
					transaction.Rollback ();
					throw;
				}
			}
		}

		void UpdateXGMLabels (UniqueId uid, IReadOnlySetOfStrings labels)
		{
			var oldLabels = new HashSet<string> (StringComparer.OrdinalIgnoreCase);

			LoadXGMLabels (uid, oldLabels);

			using (var transaction = sqlite.BeginTransaction ()) {
				try {
					foreach (var label in oldLabels) {
						if (labels.Contains (label))
							continue;

						using (var command = sqlite.CreateCommand ()) {
							command.CommandText = $"DELETE FROM {GMailLabelsTable.TableName} WHERE UID = @UID AND LABEL = @LABEL";
							command.Parameters.AddWithValue ("@UID", (long) uid.Id);
							command.Parameters.AddWithValue ("@LABEL", label);
							command.CommandType = CommandType.Text;

							command.ExecuteNonQuery ();
						}
					}

					foreach (var label in labels) {
						if (oldLabels.Contains (label))
							continue;

						using (var command = sqlite.CreateCommand ()) {
							command.CommandText = $"INSERT INTO {GMailLabelsTable.TableName} (UID, LABEL) VALUES(@UID, @LABEL)";
							command.Parameters.AddWithValue ("@UID", (long) uid.Id);
							command.Parameters.AddWithValue ("@LABEL", label);
							command.CommandType = CommandType.Text;

							command.ExecuteNonQuery ();
						}
					}

					transaction.Commit ();
				} catch {
					transaction.Rollback ();
					throw;
				}
			}
		}

		void AddOrUpdate (UniqueId uid, IMessageSummary message)
		{
			using (var transaction = sqlite.BeginTransaction ()) {
				try {
					using (var command = sqlite.CreateCommand ()) {
						var columns = GetMessageTableColumns (message.Fields & ~MessageSummaryItems.UniqueId);
						var builder = new StringBuilder ($"INSERT INTO {MessageTable.TableName} (UID, FETCHED");

						for (int i = 0; i < columns.Count; i++) {
							builder.Append (", ");
							builder.Append (columns[i]);
						}

						builder.Append (") VALUES(@UID, @FETCHED");
						command.Parameters.AddWithValue ("@UID", (long) uid.Id);
						command.Parameters.AddWithValue ("@FETCHED", (int) message.Fields);

						for (int i = 0; i < columns.Count; i++) {
							var value = GetValue (uid, message, columns[i]);
							var variable = "@" + columns[i];

							builder.Append (", ");
							builder.Append (variable);
							command.Parameters.AddWithValue (variable, value);
						}

						builder.Append (") ON CONFLICT(UID) DO UPDATE SET FETCHED = FETCHED | @FETCHED");

						for (int i = 0; i < columns.Count; i++)
							builder.AppendFormat (", {0} = @{0}", columns[i]);

						command.CommandText = builder.ToString ();
						command.CommandType = CommandType.Text;

						command.ExecuteNonQuery ();
					}

					if ((message.Fields & MessageSummaryItems.Flags) != 0)
						UpdateKeywords (uid, message.Keywords);

					if ((message.Fields & MessageSummaryItems.GMailLabels) != 0) {
						var labels = new HashSet<string> (message.GMailLabels);

						UpdateXGMLabels (uid, labels);
					}

					transaction.Commit ();
				} catch {
					transaction.Rollback ();
					throw;
				}
			}
		}

		public void AddOrUpdate (IMessageSummary message)
		{
			UniqueId uid;

			if (message.UniqueId.IsValid)
				uid = message.UniqueId;
			else if (!TryGetUniqueId (message.Index, out uid))
				return;

			AddOrUpdate (uid, message);
			IsDirty = true;
		}

		DbCommand CreateExpungeMessageCommand (UniqueId uid)
		{
			var command = sqlite.CreateCommand ();
			command.CommandText = $"DELETE FROM {MessageTable.TableName} WHERE UID = @UID";
			command.Parameters.AddWithValue ("@UID", (long) uid.Id);
			command.CommandType = CommandType.Text;
			return command;
		}

		DbCommand CreateExpungeKeywordsCommand (UniqueId uid)
		{
			var command = sqlite.CreateCommand ();
			command.CommandText = $"DELETE FROM {KeywordsTable.TableName} WHERE UID = @UID";
			command.Parameters.AddWithValue ("@UID", (long) uid.Id);
			command.CommandType = CommandType.Text;
			return command;
		}

		DbCommand CreateExpungeXGMLabelsCommand (UniqueId uid)
		{
			var command = sqlite.CreateCommand ();
			command.CommandText = $"DELETE FROM {GMailLabelsTable.TableName} WHERE UID = @UID";
			command.Parameters.AddWithValue ("@UID", (long) uid.Id);
			command.CommandType = CommandType.Text;
			return command;
		}

		void Expunge (UniqueId uid, CancellationToken cancellationToken = default)
		{
			using (var transaction = sqlite.BeginTransaction ()) {
				try {
					using (var command = CreateExpungeMessageCommand (uid))
						command.ExecuteNonQuery ();

					using (var command = CreateExpungeKeywordsCommand (uid))
						command.ExecuteNonQuery ();

					using (var command = CreateExpungeXGMLabelsCommand (uid))
						command.ExecuteNonQuery ();

					transaction.Commit ();
				} catch {
					transaction.Rollback ();
					throw;
				}
			}
		}

		public void Expunge (int index)
		{
			if (TryGetUniqueId (index, out var uid))
				Expunge (uid);
		}

		public void Expunge (IEnumerable<UniqueId> uids)
		{
			foreach (var uid in uids)
				Expunge (uid);
		}

		static List<string> GetMessageTableColumns (MessageSummaryItems items)
		{
			var columns = new List<string> ();

			if ((items & MessageSummaryItems.UniqueId) != 0)
				columns.Add ("UID");
			if ((items & MessageSummaryItems.InternalDate) != 0) {
				columns.Add ("INTERNALDATE");
				columns.Add ("INTERNALTIMEZONE");
			}
			if ((items & MessageSummaryItems.Size) != 0)
				columns.Add ("SIZE");
			if ((items & MessageSummaryItems.Flags) != 0)
				columns.Add ("FLAGS");
			if ((items & MessageSummaryItems.ModSeq) != 0)
				columns.Add ("MODSEQ");
			if ((items & MessageSummaryItems.Envelope) != 0) {
				columns.Add ("DATE");
				columns.Add ("TIMEZONE");
				columns.Add ("SUBJECT");
				columns.Add ("FROM");
				columns.Add ("SENDER");
				columns.Add ("REPLYTO");
				columns.Add ("TO");
				columns.Add ("CC");
				columns.Add ("BCC");
				columns.Add ("INREPLYTO");
				columns.Add ("MESSAGEID");
			}
			if ((items & MessageSummaryItems.References) != 0)
				columns.Add ("REFERENCES");
			if ((items & (MessageSummaryItems.BodyStructure | MessageSummaryItems.Body)) != 0)
				columns.Add ("BODYSTRUCTURE");
			if ((items & MessageSummaryItems.PreviewText) != 0)
				columns.Add ("PREVIEWTEXT");
			if ((items & MessageSummaryItems.GMailMessageId) != 0)
				columns.Add ("XGMMSGID");
			if ((items & MessageSummaryItems.GMailThreadId) != 0)
				columns.Add ("XGMTHRID");
			if ((items & MessageSummaryItems.EmailId) != 0)
				columns.Add ("EMAILID");
			if ((items & MessageSummaryItems.ThreadId) != 0)
				columns.Add ("THREADID");
			//if ((items & MessageSummaryItems.SaveDate) != 0) {
			//	columns.Add("SAVEDATE");
			//	columns.Add("SAVEDATETIMEZONE");
			//}

			return columns;
		}

		static DateTimeOffset GetDateTimeOffset (DateTime utc, long timeZone)
		{
			var dateTime = new DateTime (utc.Ticks, DateTimeKind.Unspecified);
			var offset = new TimeSpan (timeZone);

			dateTime = dateTime.Add (offset);

			return new DateTimeOffset (dateTime, offset);
		}

		static void LoadInternetAddressList (InternetAddressList list, DbDataReader reader, int column)
		{
			try {
				var addresses = reader.GetInternetAddressList (column);
				list.AddRange (addresses);
				addresses.Clear ();
			} catch {
			}
		}

		void LoadMessages (List<IMessageSummary> messages, MessageSummaryItems items, DbDataReader reader, int startIndex)
		{
			int index = startIndex;

			while (reader.Read ()) {
				var message = new MessageSummary (index++);
				var internalDate = InvalidDateTime;
				//var saveDate = InvalidDateTime;
				long internalTimeZone = -1;
				//long saveDateTimeZone = -1;
				var date = InvalidDateTime;
				long timeZone = -1;

				messages.Add (message);

				if ((items & MessageSummaryItems.Envelope) != 0)
					message.Envelope = new Envelope ();

				for (int i = 0; i < reader.FieldCount; i++) {
					if (reader.IsDBNull (i))
						continue;

					switch (reader.GetName (i)) {
					case "UID":
						message.UniqueId = reader.GetUniqueId (i, UidValidity!.Value);
						break;
					case "INTERNALDATE":
						internalDate = reader.GetDateTime (i);
						break;
					case "INTERNALTIMEZONE":
						internalTimeZone = reader.GetInt64 (i);
						break;
					case "SIZE":
						message.Size = (uint) reader.GetInt64 (i);
						break;
					case "FLAGS":
						message.Flags = reader.GetMessageFlags (i);
						break;
					case "MODSEQ":
						message.ModSeq = reader.GetUInt64 (i);
						break;
					case "DATE":
						date = reader.GetDateTime (i);
						break;
					case "TIMEZONE":
						timeZone = reader.GetInt64 (i);
						break;
					case "SUBJECT":
						message.Envelope.Subject = reader.GetString (i);
						break;
					case "FROM":
						LoadInternetAddressList (message.Envelope.From, reader, i);
						break;
					case "SENDER":
						LoadInternetAddressList (message.Envelope.Sender, reader, i);
						break;
					case "REPLYTO":
						LoadInternetAddressList (message.Envelope.ReplyTo, reader, i);
						break;
					case "TO":
						LoadInternetAddressList (message.Envelope.To, reader, i);
						break;
					case "CC":
						LoadInternetAddressList (message.Envelope.Cc, reader, i);
						break;
					case "BCC":
						LoadInternetAddressList (message.Envelope.Bcc, reader, i);
						break;
					case "INREPLYTO":
						message.Envelope.InReplyTo = reader.GetString (i);
						break;
					case "MESSAGEID":
						message.Envelope.MessageId = reader.GetString (i);
						break;
					case "REFERENCES":
						message.References = reader.GetReferences (i);
						break;
					case "BODYSTRUCTURE":
						message.Body = reader.GetBodyStructure (i);
						break;
					case "PREVIEWTEXT":
						message.PreviewText = reader.GetString (i);
						break;
					case "XGMMSGID":
						message.GMailMessageId = reader.GetUInt64 (i);
						break;
					case "XGMTHRID":
						message.GMailThreadId = reader.GetUInt64 (i);
						break;
					case "EMAILID":
						message.EmailId = reader.GetString (i);
						break;
					case "THREADID":
						message.ThreadId = reader.GetString (i);
						break;
						//case "SAVEDATE":
						//	saveDate = reader.GetDateTime(i);
						//	break;
						//case "SAVEDATETIMEZONE":
						//	saveDateTimeZone = reader.GetInt64(i);
						//	break;
					}
				}

				if (internalDate != InvalidDateTime)
					message.InternalDate = GetDateTimeOffset (internalDate, internalTimeZone);

				//if (saveDate != InvalidDateTime)
				//	message.SaveDate = GetDateTimeOffset(saveDate, saveDateTimeZone);

				if (date != InvalidDateTime)
					message.Envelope.Date = GetDateTimeOffset (date, timeZone);
			}
		}

		void LoadKeywords (UniqueId uid, ISet<string> keywords)
		{
			using (var command = sqlite.CreateCommand ()) {
				command.CommandText = $"SELECT KEYWORD FROM {KeywordsTable.TableName} WHERE UID = @UID";
				command.Parameters.AddWithValue ("@UID", (long) uid.Id);
				command.CommandType = CommandType.Text;

				using (var reader = command.ExecuteReader ()) {
					while (reader.Read ()) {
						var column = reader.GetOrdinal ("KEYWORD");

						if (column != -1)
							keywords.Add (reader.GetString (column));
					}
				}
			}
		}

		void LoadXGMLabels (UniqueId uid, ISet<string> labels)
		{
			using (var command = sqlite.CreateCommand ()) {
				command.CommandText = $"SELECT LABEL FROM {GMailLabelsTable.TableName} WHERE UID = @UID";
				command.Parameters.AddWithValue ("@UID", (long) uid.Id);
				command.CommandType = CommandType.Text;

				using (var reader = command.ExecuteReader ()) {
					while (reader.Read ()) {
						var column = reader.GetOrdinal ("LABEL");

						if (column != -1)
							labels.Add (reader.GetString (column));
					}
				}
			}
		}

		static bool IsEmptyFetchRequest (IFetchRequest request)
		{
			return request.Items == MessageSummaryItems.None && (request.Headers == null || (request.Headers.Count == 0 && !request.Headers.Exclude));
		}

		public IList<IMessageSummary> Fetch (int min, int max, IFetchRequest request)
		{
			if (min < 0)
				throw new ArgumentOutOfRangeException (nameof (min));

			if (max != -1 && max < min)
				throw new ArgumentOutOfRangeException (nameof (max));

			if (request == null)
				throw new ArgumentNullException (nameof (request));

			if (Count == 0 || IsEmptyFetchRequest (request))
				return Array.Empty<IMessageSummary> ();

			int capacity = Math.Max (max < 0 || max > Count ? Count : max, min) - min;
			var messages = new List<IMessageSummary> (capacity);
			var items = request.Items;

			if ((items & (MessageSummaryItems.Flags /*| MessageSummaryItems.Annotations*/)) != 0)
				items |= MessageSummaryItems.UniqueId;

			using (var command = sqlite.CreateCommand ()) {
				var columns = GetMessageTableColumns (items);
				var builder = new StringBuilder ("SELECT ");

				if (columns.Count > 0) {
					foreach (var column in columns)
						builder = builder.Append (column).Append (", ");

					builder.Length -= 2;
				} else {
					builder.Append ("UID");
				}

				builder.Append ($"FROM {MessageTable.TableName} ");

				if (request.ChangedSince.HasValue) {
					command.Parameters.AddWithValue ("@MODSEQ", request.ChangedSince.Value);
					builder.Append ("WHERE MODSEQ > @MODSEQ ");
				}

				builder.Append ("ORDER BY UID");

				if (max != -1) {
					command.Parameters.AddWithValue ("@LIMIT", capacity);
					builder.Append (" LIMIT @LIMIT");
				}

				if (min > 0) {
					command.Parameters.AddWithValue ("@OFFSET", min);
					builder.Append (" OFFSET @OFFSET");
				}

				command.CommandText = builder.ToString ();
				command.CommandType = CommandType.Text;

				using (var reader = command.ExecuteReader ())
					LoadMessages (messages, items, reader, min);
			}

			if ((items & MessageSummaryItems.Flags) != 0) {
				var keywords = new HashSet<string> ();

				foreach (var message in messages) {
					LoadKeywords (message.UniqueId, keywords);

					foreach (var keyword in keywords)
						((HashSet<string>) message.Keywords).Add (keyword);

					keywords.Clear ();
				}
			}

			if ((items & MessageSummaryItems.GMailLabels) != 0) {
				var labels = new HashSet<string> ();

				foreach (var message in messages) {
					LoadXGMLabels (message.UniqueId, labels);

					foreach (var label in labels)
						message.GMailLabels.Add (label);

					labels.Clear ();
				}
			}

			return messages;
		}

		public IList<IMessageSummary> Fetch (IList<int> indexes, IFetchRequest request)
		{
			if (indexes == null)
				throw new ArgumentNullException (nameof (indexes));

			if (request == null)
				throw new ArgumentNullException (nameof (request));

			if (indexes.Count == 0 || Count == 0 || IsEmptyFetchRequest (request))
				return Array.Empty<IMessageSummary> ();

			return null;
		}

		public IList<IMessageSummary> Fetch (IList<UniqueId> uids, IFetchRequest request)
		{
			if (uids == null)
				throw new ArgumentNullException (nameof (uids));

			if (request == null)
				throw new ArgumentNullException (nameof (request));

			if (uids.Count == 0 || Count == 0 || IsEmptyFetchRequest (request))
				return Array.Empty<IMessageSummary> ();

			return null;
		}

		public void Dispose ()
		{
			if (sqlite != null)
				Close ();
		}
	}
}
