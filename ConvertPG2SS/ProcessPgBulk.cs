﻿//----------------------------------------------------------------------------------------
// <copyright company="">
//    Copyright 2015 Miguel Vrolijk.
//
//    This file is part of ConvertPG2SS.
//
//    ConvertPG2SS is free software: you can redistribute it and/or modify
//    it under the terms of the GNU General Public License as published by
//    the Free Software Foundation, either version 3 of the License, or
//    (at your option) any later version.
//
//    ConvertPG2SS is distributed in the hope that it will be useful,
//    but WITHOUT ANY WARRANTY; without even the implied warranty of
//    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
//    GNU General Public License for more details.
//
//    You should have received a copy (COPYING.txt) of the GNU General Public
//    License along with ConvertPG2SS.  If not, see
//    <http://www.gnu.org/licenses/>.
// </copyright>
// <author>Miguel Vrolijk</author> 
// <date>2015-09-25</date>
// <time>19:17</time>
//
// <summary>Process the Postgres db bulk conversion.</summary>
//----------------------------------------------------------------------------------------

using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.IO;
using System.Text;
using ConvertPG2SS.Common;
using ConvertPG2SS.Interfaces;
using Npgsql;

namespace ConvertPG2SS {
	static class ProcessPgBulk {
		private static IBLogger _log;
		private static IParameters _params;

		internal static void Do() {
			_log = Program.GetInstance<IBLogger>();
			_params = Program.GetInstance<IParameters>();

			var frmConn = (NpgsqlConnection)_params[Constants.PgConnection];

			CreateBulkFile(frmConn);
			CreateImportFiles(frmConn);

		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="conn"></param>
		private static void CreateBulkFile(NpgsqlConnection conn) {
			const string sql =
				@"SELECT schema_name, table_name
				FROM	 public.tables
				ORDER BY schema_name, table_name";

			var path = Path.Combine(
				_params["other.work_path"].ToString(),
				"02_bulk_copy.sql");

			using (var sw = new StreamWriter(path, false, Encoding.Default)) 
			using (var cmd = new NpgsqlCommand(sql, conn)) {
				sw.WriteLine("USE " + _params["mssql.database"] + ";");
				sw.WriteLine("GO");
				sw.WriteLine();
				sw.WriteLine("BEGIN TRANSACTION;");
				sw.WriteLine();

				using (var reader = cmd.ExecuteReader()) {
					while (reader.Read()) {
						var schema = reader["schema_name"].ToString();
						var table = reader["table_name"].ToString();

						sw.WriteLine("BULK");
						sw.WriteLine("INSERT [" + schema + "].[" + table + "]");

						sw.WriteLine("FROM '" + ImportFile(schema, table) + "'");
						sw.WriteLine("WITH (FIELDTERMINATOR = '\\t', ROWTERMINATOR = '\\n')");
						sw.WriteLine("GO");
						sw.WriteLine();
					}
				}
				sw.WriteLine("COMMIT TRANSACTION;");
			}
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="conn"></param>
		private static void CreateImportFiles(NpgsqlConnection conn) {
			const string sql =
				@"SELECT schema_name, table_name
				FROM	 public.tables
				ORDER BY schema_name, table_name";

			using (var da = new NpgsqlDataAdapter(sql, conn)) {
				var dt = new DataTable();
				da.Fill(dt);
				if (dt.Rows.Count == 0) return;

				_log.Info("");
				_log.Write('I', Constants.LogTsType, "Generating import files:");
				long totalRecCount = 0;

				foreach (DataRow row in dt.Rows) {
					var schema = row["schema_name"].ToString();
					var table = row["table_name"].ToString();
					using (var sw = new StreamWriter(ImportFile(schema, table), false, Encoding.Default)) {
						totalRecCount += sw.CreateImportFile(schema, table, conn);
					}
				}
				dt.Dispose();
				_log.Info("");
				_log.Write(
					'I', 
					Constants.LogTsType, 
					string.Format(
						CultureInfo.InvariantCulture,
						"Total records processed: {0,13:n0}",
						totalRecCount));
			}
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="sw"></param>
		/// <param name="schema"></param>
		/// <param name="table"></param>
		/// <param name="conn"></param>
		private static int CreateImportFile(
			this TextWriter sw, 
			string schema, 
			string table, 
			NpgsqlConnection conn) 
		{
			var limit = int.Parse(_params["postgres.limit"].ToString());
			string sql;

			if (limit > 0) {
				sql = string.Format(
					CultureInfo.InvariantCulture,
					"SELECT * FROM {0}.{1} LIMIT {2}",
					schema, table, limit);
			}
			else {
				sql = string.Format(
					CultureInfo.InvariantCulture,
					"SELECT * FROM {0}.{1}",
					schema, table);
			}

			var criteria = string.Format(
				CultureInfo.InvariantCulture,
				"schema_name = '{0}' AND table_name = '{1}'",
				schema, table);

			var dt = (DataTable) _params[Constants.PgSchemaTable];
			NpgsqlCommand cmd = null;
			NpgsqlDataReader reader = null;
			var colInfo = dt.Select(criteria, "column_index");
			var recCount = 0;

			try {
				cmd = new NpgsqlCommand(sql, conn);
				reader = cmd.ExecuteReader();

				while (reader.Read()) {
					sw.WriteLine(ProcessRow(reader, colInfo));
					recCount++;
					//if (recCount%100000 == 0) {
					//	sw.Flush();
					//	_log.Write('T', Constants.LogTsType, recCount.ToString("N0"), 2);
					//}
				}
			}
			catch (NpgsqlException ex) {
				_log.WriteEx('F', Constants.LogTsType, ex);
			}
			finally {
				if (reader != null) reader.Dispose();
				if (cmd != null) cmd.Dispose();
			}

			var pad = (49 - schema.Length - table.Length);
			string qualName;

			if (pad > 0) qualName = schema + "." + table + new string('.', pad);
			else qualName = schema + "." + table;

			_log.Write(
				'I',
				Constants.LogTsType,
				string.Format(
					CultureInfo.InvariantCulture,
					"{0}: {1,13:n0}",
					qualName, recCount),
				1);

			return recCount;
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="reader"></param>
		/// <param name="colInfo"></param>
		/// <returns></returns>
		private static string ProcessRow(IDataRecord reader, IReadOnlyList<DataRow> colInfo) {
			var sb = new StringBuilder();

			for (var i = 0; i < reader.FieldCount; i++) {
				var column = colInfo[i];

				if (i > 0) sb.Append(Constants.Tab);
				var valueType = reader[i].GetType();
				if (valueType.IsArray) {
					var enumerable = reader[i] as IEnumerable;
					if (enumerable == null) {
						sb.Append(FormatColumnVal(reader[i], column));
						continue;
					}
					var j = 0;
					foreach (var val in enumerable) {
						if (j > 0) sb.Append(Constants.Tab);
						sb.Append(FormatColumnVal(val, column));
						j++;
					}
				}
				else sb.Append(FormatColumnVal(reader[i], column));
			}
			return sb.ToString();
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="obj"></param>
		/// <param name="col"></param>
		/// <returns></returns>
		private static string FormatColumnVal(object obj, DataRow col) {
			if (obj == DBNull.Value || obj == null) {
				return (bool)col["notnull"] ? "NULL" :"";
			}
			switch (col["data_type"].ToString()) {
				case "date":
					return ((DateTime) obj).ToString(Constants.IsoDate);
				case "timestamp without time zone":
				case "timestamp with time zone":
				case "timestamp without time zone[]":
				case "timestamp with time zone[]":
					return ((DateTime)obj).ToString(Constants.TimeStamp);
				default:
					return obj.ToString();
			}
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="schema"></param>
		/// <param name="table"></param>
		/// <returns></returns>
		private static string ImportFile(string schema, string table) {
			return Path.Combine(
			 _params["other.dump_path"].ToString(),
			 schema + "_" + table + ".tsv");
		}
	}
}
