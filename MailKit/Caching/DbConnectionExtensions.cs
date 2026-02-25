//
// DbConnectionExtensions.cs
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
using System.Data;
using System.Text;
using System.Threading;
using System.Data.Common;
using System.Threading.Tasks;

namespace MailKit.Caching {
	static class DbConnectionExtensions
	{
		static void Build (StringBuilder command, DataTable table, DataColumn column, ref int primaryKeys, bool addColumn)
		{
			command.Append (column.ColumnName);
			command.Append (' ');

			if (column.DataType == typeof (long) || column.DataType == typeof (int) || column.DataType == typeof (bool)) {
				command.Append ("INTEGER");
			} else if (column.DataType == typeof (byte[])) {
				command.Append ("BLOB");
			} else if (column.DataType == typeof (DateTime)) {
				command.Append ("DATE");
			} else if (column.DataType == typeof (string)) {
				command.Append ("TEXT");
			} else {
				throw new NotImplementedException ();
			}

			bool isPrimaryKey = false;
			if (table != null && table.PrimaryKey != null && primaryKeys < table.PrimaryKey.Length) {
				for (int i = 0; i < table.PrimaryKey.Length; i++) {
					if (column == table.PrimaryKey[i]) {
						command.Append (" PRIMARY KEY");
						isPrimaryKey = true;
						primaryKeys++;
						break;
					}
				}
			}

			if (column.AutoIncrement)
				command.Append (" AUTOINCREMENT");

			if (column.Unique && !isPrimaryKey)
				command.Append (" UNIQUE");

			// Note: Normally we'd want to include NOT NULL, but we can't *add* new columns with the NOT NULL restriction
			if (!addColumn && !column.AllowDBNull)
				command.Append (" NOT NULL");
		}

		static string GetCreateTableCommand (DataTable table)
		{
			var command = new StringBuilder ("CREATE TABLE IF NOT EXISTS ");
			int primaryKeys = 0;

			command.Append (table.TableName);
			command.Append ('(');

			foreach (DataColumn column in table.Columns) {
				Build (command, table, column, ref primaryKeys, false);
				command.Append (", ");
			}

			if (table.Columns.Count > 0)
				command.Length -= 2;

			command.Append (')');

			return command.ToString ();
		}

		public static void CreateTable (this DbConnection connection, DataTable table)
		{
			using (var command = connection.CreateCommand ()) {
				command.CommandText = GetCreateTableCommand (table);
				command.CommandType = CommandType.Text;
				command.ExecuteNonQuery ();
			}
		}

		public static async Task CreateTableAsync (this DbConnection connection, DataTable table, CancellationToken cancellationToken)
		{
			using (var command = connection.CreateCommand ()) {
				command.CommandText = GetCreateTableCommand (table);
				command.CommandType = CommandType.Text;

				await command.ExecuteNonQueryAsync (cancellationToken).ConfigureAwait (false);
			}
		}

		static string GetAddColumnCommand (DataTable table, DataColumn column)
		{
			var command = new StringBuilder ("ALTER TABLE ");
			int primaryKeys = table.PrimaryKey?.Length ?? 0;

			command.Append (table.TableName);
			command.Append (" ADD COLUMN ");
			Build (command, table, column, ref primaryKeys, true);

			return command.ToString ();
		}

		public static void AddTableColumn (this DbConnection connection, DataTable table, DataColumn column)
		{
			using (var command = connection.CreateCommand ()) {
				command.CommandText = GetAddColumnCommand (table, column);
				command.CommandType = CommandType.Text;
				command.ExecuteNonQuery ();
			}
		}

		public static async Task AddTableColumnAsync (this DbConnection connection, DataTable table, DataColumn column, CancellationToken cancellationToken)
		{
			using (var command = connection.CreateCommand ()) {
				command.CommandText = GetAddColumnCommand (table, column);
				command.CommandType = CommandType.Text;

				await command.ExecuteNonQueryAsync (cancellationToken).ConfigureAwait (false);
			}
		}
	}
}
