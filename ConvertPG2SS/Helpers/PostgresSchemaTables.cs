//----------------------------------------------------------------------------------------
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
// <date>2015-09-29</date>
// <time>14:14</time>
//
// <summary>Load PostgreSQL specific schema data into DataTables.</summary>
//----------------------------------------------------------------------------------------

using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Linq;
using System.Text;
using ConvertPG2SS.Common;
using ConvertPG2SS.Interfaces;
using Npgsql;

namespace ConvertPG2SS.Helpers {
	internal static class PostgresSchemaTables {
		private static IBLogger _log;
		private static IParameters _params;
		private static NpgsqlConnection _pgConn;

		private const string DimPostfix = "_dim";

		/// <summary>
		/// 
		/// </summary>
		internal static void CreateTables() {
			_log = Program.GetInstance<IBLogger>();
			_params = Program.GetInstance<IParameters>();
			_pgConn = (NpgsqlConnection) _params[Constants.PgConnection];

			CreateSchemaTable();
			CreateTypeTable();
			CreateSequenceTable();
			CreateFkTable();
		}

		/// <summary>
		/// 
		/// </summary>
		private static void CreateSchemaTable() {
			#region PostgreSQL query to retrieve coloumn information from pg_catalog.

			var inclPublic = bool.Parse(_params[Parameters.PostgresIncludePublic].ToString());
			var inList = "'pg_catalog', 'information_schema'";
			if (!inclPublic) inList += ", 'public'";

			var sql =
				string.Format(
					CultureInfo.InvariantCulture,
					@"SELECT n.nspname AS schema_name, c.relname AS table_name,
				a.attname AS column_name, a.attnum AS column_index, 
				(a.atttypid::regtype)::text AS regtype, a.attnotnull AS notnull,
				format_type(a.atttypid, a.atttypmod) AS data_type, a.attndims AS dims,
				CASE
					WHEN a.atttypid = ANY (ARRAY[1042::oid, 1043::oid, 1015::oid]) THEN
						CASE 
							WHEN a.atttypmod > 0 THEN a.atttypmod - 4
							ELSE -1
						END
				END AS max_char_size,
				CASE a.atttypid
					WHEN 21 THEN 16
					WHEN 23 THEN 32
					WHEN 20 THEN 64
					WHEN 1700 THEN
					CASE
						WHEN a.atttypmod = (-1) THEN NULL::integer
						ELSE ((a.atttypmod - 4) >> 16) & 65535
					END
					WHEN 700 THEN 24
					WHEN 701 THEN 53
					ELSE NULL::integer
				END AS numeric_precision,
				CASE
					WHEN a.atttypid = ANY (ARRAY[21::oid, 23::oid, 20::oid]) THEN 0
					WHEN a.atttypid = 1700::oid THEN
						CASE
							WHEN a.atttypmod = (-1) THEN NULL::integer
							ELSE (a.atttypmod - 4) & 65535
						END
					ELSE NULL::integer
				END AS numeric_scale,
				d.adsrc AS default_val,
				(SELECT col_description(a.attrelid, a.attnum::integer) AS col_description)
					AS comment
				FROM pg_class c
					LEFT JOIN pg_namespace n ON n.oid = c.relnamespace
					LEFT JOIN pg_tablespace t ON t.oid = c.reltablespace
					LEFT JOIN pg_attribute a ON a.attrelid = c.oid AND a.attnum > 0
					LEFT JOIN pg_attrdef d ON d.adrelid = a.attrelid AND d.adnum = a.attnum
				WHERE c.relkind = 'r'::""char"" AND n.nspname NOT IN({0})
				ORDER BY n.nspname ASC, c.relname ASC, a.attnum ASC",
					inList);

			#endregion

			var tblDict = ((Dictionary<string, DataTable>) _params[Constants.PgTables]);
			var dt = tblDict[Constants.PgSchemaTable];

			NpgsqlDataAdapter da = null;

			try {
				da = new NpgsqlDataAdapter(sql, _pgConn);
				da.Fill(dt);
			}
			catch (NpgsqlException ex) {
				_log.WriteEx('E', Constants.LogTsType, ex);
			}
			finally {
				da?.Dispose();
			}

			if (dt.Rows.Count == 0) return;

			// Add a dim size coloumn.
			dt.Columns.Add("dim_size", typeof(int));

			// Add primary key to table.
			var columns = new DataColumn[3];
			columns[0] = dt.Columns["schema_name"];
			columns[1] = dt.Columns["table_name"];
			columns[2] = dt.Columns["column_name"];
			dt.PrimaryKey = columns;

			ProcessDimensions(dt);
		}

		/// <summary>
		///     Process all the tables that have bytea or array columns.
		/// </summary>
		/// <param name="dt"></param>
		private static void ProcessDimensions(DataTable dt) {
			var dts = dt.Select("dims <> 0 OR regtype = 'bytea'");

			// Select all the tables that have at least one dimensioned column. 
			var tables = dts
				.Select(m => new {
					sn = m.Field<string>("schema_name"),
					tb = m.Field<string>("table_name")
				}).Distinct();

			foreach (var dimRows in tables.Select(table => string.Format(
				CultureInfo.InvariantCulture,
				"schema_name = '{0}' AND table_name = '{1}' AND " +
				"(dims <> 0 OR regtype = 'bytea')",
				table.sn, table.tb)).Select(dt.Select)) {
				ProcessDimRows(dimRows);
			}
		}

		/// <summary>
		///     Procces the bytea or array columns.
		/// </summary>
		/// <param name="dr"></param>
		private static void ProcessDimRows(DataRow[] dr) {
			var sql = BuildCommand(dr);

			var dt = new DataTable();
			NpgsqlDataAdapter da = null;

			try {
				da = new NpgsqlDataAdapter(sql, _pgConn);
				da.Fill(dt);

				if (dt.Rows.Count == 0) return;

				var dimRow = dt.Rows[0];

				foreach (var row in dr) {
					var rowName = row["column_name"] + DimPostfix;
					if (row["regtype"].ToString() != "bytea")
						row["dim_size"] = dimRow[rowName];
					else row["max_char_size"] = dimRow[rowName];
				}
			}
			catch (NpgsqlException ex) {
				_log.WriteEx('E', Constants.LogTsType, ex);
			}
			finally {
				dt.Dispose();
				da?.Dispose();
			}

		}

		/// <summary>
		///     Build the SQL SELECT statement to determine maximum array dimensions and
		///     bytea maximum size.
		/// </summary>
		/// <param name="dr"></param>
		/// <returns></returns>
		private static string BuildCommand(IEnumerable<DataRow> dr) {
			var sb = new StringBuilder();
			var first = true;
			var table = "";
			var lim = int.Parse(_params[Parameters.PostgresArrayLimit].ToString());

			sb.Append("SELECT ");

			// TODO: 2015-10-10: what if you have a bytea array?
			foreach (var row in dr) {
				if (first) {
					table = row["schema_name"] + "." + row["table_name"];
					first = false;
				}
				else sb.Append(", ");

				sb.Append(row["regtype"].ToString() == "bytea"
					? "max(length("
					: "max(cardinality(");
				if (lim > 0) sb.Append("t.");
				sb.Append(row["column_name"] + "))");
				sb.Append(" AS " + row["column_name"] + DimPostfix);
			}

			sb.Append(" FROM ");

			if (lim > 0) {
				sb.Append("( SELECT * FROM " + table + " LIMIT " + lim);
				sb.Append(") t");
			}
			else {
				sb.Append(table);
			}

			return sb.ToString();
		}

		/// <summary>
		/// 
		/// </summary>
		private static void CreateTypeTable() {
			#region PostgreSQL query to retrieve type/domain information from pg_catalog.

			const string sql =
				@"SELECT n.nspname as schema_name, format_type(t.oid, NULL) AS type_name,
				(typbasetype::regtype)::text AS regtype, typnotnull AS notnull,
				format_type(typbasetype, typtypmod) AS data_type, typndims AS dims,
					CASE
						WHEN typbasetype = ANY (ARRAY[1042::oid, 1043::oid, 1015::oid]) THEN
						CASE 
							WHEN typtypmod > 0 THEN typtypmod - 4
							ELSE NULL::integer
						END
					END AS max_char_size,
					CASE typbasetype
						WHEN 21 THEN 16
						WHEN 23 THEN 32
						WHEN 20 THEN 64
						WHEN 1700 THEN
						CASE
							WHEN typtypmod = (-1) THEN NULL::integer
							ELSE ((typtypmod - 4) >> 16) & 65535
						END
						WHEN 700 THEN 24
						WHEN 701 THEN 53
						ELSE NULL::integer
					END AS numeric_precision,
					CASE
						WHEN typbasetype = ANY (ARRAY[21::oid, 23::oid, 20::oid]) THEN 0
						WHEN typbasetype = 1700::oid THEN
							CASE
								WHEN typtypmod = (-1) THEN NULL::integer
								ELSE (typtypmod - 4) & 65535
							END
						ELSE NULL::integer
					END AS numeric_scale,
					typdefault as default_val, typisdefined, typdelim,
					obj_description(t.oid, 'pg_type') as comment
				FROM pg_type t
					 LEFT JOIN pg_namespace n ON n.oid = t.typnamespace
				WHERE (t.typrelid = 0 OR 
					   (SELECT c.relkind = 'c' FROM pg_class c WHERE c.oid = t.typrelid))
					  AND NOT EXISTS(SELECT 1 FROM pg_type el WHERE el.oid = t.typelem 
									 AND el.typarray = t.oid)
					  AND pg_type_is_visible(t.oid) 
					  AND nspname <> 'pg_catalog' AND typbasetype <> 0 ORDER BY 1, 2";

			#endregion

			var tblDict = ((Dictionary<string, DataTable>) _params[Constants.PgTables]);
			var dt = tblDict[Constants.PgTypeTable];

			NpgsqlDataAdapter da = null;

			try {
				da = new NpgsqlDataAdapter(sql, _pgConn);
				da.Fill(dt);
			}
			catch (NpgsqlException ex) {
				_log.WriteEx('E', Constants.LogTsType, ex);
			}
			finally {
				da?.Dispose();
			}
			
			if (dt.Rows.Count == 0) return;

			// Add primary key to table.
			var columns = new DataColumn[2];
			columns[0] = dt.Columns["schema_name"];
			columns[1] = dt.Columns["type_name"];
			dt.PrimaryKey = columns;
		}

		/// <summary>
		/// 
		/// </summary>
		private static void CreateSequenceTable() {
			var inclPublic = bool.Parse(_params[Parameters.PostgresIncludePublic].ToString());
			var inList = "'pg_catalog', 'information_schema'";
			if (!inclPublic) inList += ", 'public'";

			var sql =
				string.Format(
					CultureInfo.InvariantCulture,
					@"SELECT n.nspname AS schema_name, c.relname AS seq_name
					FROM	pg_class c
						JOIN pg_namespace n ON c.relnamespace = n.oid
					WHERE	c.relkind = 'S' AND n.nspname NOT IN({0})
					ORDER	BY n.nspname ASC, c.relname ASC",
					inList);

			var tblDict = ((Dictionary<string, DataTable>)_params[Constants.PgTables]);
			var dt = tblDict[Constants.PgSeqTable];

			NpgsqlDataAdapter da = null;

			try {
				da = new NpgsqlDataAdapter(sql, _pgConn);
				da.Fill(dt);
			}
			catch (NpgsqlException ex) {
				_log.WriteEx('E', Constants.LogTsType, ex);
			}
			finally {
				da?.Dispose();
			}

			if (dt.Rows.Count == 0) return;

			// Add sequence specification columns.
			dt.Columns.Add("last_value", typeof(long));
			dt.Columns.Add("start_value", typeof(long));
			dt.Columns.Add("increment_by", typeof(long));
			dt.Columns.Add("max_value", typeof(long));
			dt.Columns.Add("min_value", typeof(long));
			dt.Columns.Add("cache_value", typeof(long));
			dt.Columns.Add("log_cnt", typeof(long));
			dt.Columns.Add("is_cycled", typeof(bool));
			dt.Columns.Add("is_called", typeof(bool));

			// Add primary key to table.
			var columns = new DataColumn[2];
			columns[0] = dt.Columns["schema_name"];
			columns[1] = dt.Columns["seq_name"];
			dt.PrimaryKey = columns;

			ReadSequenceDetails(dt);
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="dt"></param>
		private static void ReadSequenceDetails(DataTable dt) {
			foreach (DataRow row in dt.Rows) {
				var sql = string.Format(
					CultureInfo.InvariantCulture,
					"SELECT * FROM {0}.{1}",
					row["schema_name"], row["seq_name"]);

				var dtSeq = new DataTable();
				using (var da = new NpgsqlDataAdapter(sql, _pgConn)) {
					da.Fill(dtSeq);
					if (dtSeq.Rows.Count == 0) continue;

					row["last_value"] = dtSeq.Rows[0]["last_value"];
					row["start_value"] = dtSeq.Rows[0]["start_value"];
					row["increment_by"] = dtSeq.Rows[0]["increment_by"];
					row["max_value"] = dtSeq.Rows[0]["max_value"];
					row["min_value"] = dtSeq.Rows[0]["min_value"];
					row["cache_value"] = dtSeq.Rows[0]["cache_value"];
					row["log_cnt"] = dtSeq.Rows[0]["log_cnt"];
					row["is_cycled"] = dtSeq.Rows[0]["is_cycled"];
					row["is_called"] = dtSeq.Rows[0]["is_called"];

					dtSeq.Dispose();
				}
			}
		}

		/// <summary>
		/// 
		/// </summary>
		private static void CreateFkTable() {
			#region PostgreSQL query to retrieve foreign keys information from pg_catalog.

			const string sql =
				@"SELECT pg_constraint.conname fk_name,
						 ts.nspname schema_name, tt.relname table_name,
						 os.nspname fk_schema, ot.relname fk_table,
				(select attname from pg_attribute where attrelid = pg_constraint.conrelid 
					and attnum = pg_constraint.conkey[01]) key01,
				(select attname from pg_attribute where attrelid = pg_constraint.conrelid 
					and attnum = pg_constraint.conkey[02]) key02,
				(select attname from pg_attribute where attrelid = pg_constraint.conrelid 
					and attnum = pg_constraint.conkey[03]) key03,
				(select attname from pg_attribute where attrelid = pg_constraint.conrelid 
					and attnum = pg_constraint.conkey[04]) key04,
				(select attname from pg_attribute where attrelid = pg_constraint.conrelid 
					and attnum = pg_constraint.conkey[05]) key05,
				(select attname from pg_attribute where attrelid = pg_constraint.conrelid 
					and attnum = pg_constraint.conkey[06]) key06,
				(select attname from pg_attribute where attrelid = pg_constraint.conrelid 
					and attnum = pg_constraint.conkey[07]) key07,
				(select attname from pg_attribute where attrelid = pg_constraint.conrelid 
					and attnum = pg_constraint.conkey[08]) key08,
				(select attname from pg_attribute where attrelid = pg_constraint.conrelid 
					and attnum = pg_constraint.conkey[09]) key09,
				(select attname from pg_attribute where attrelid = pg_constraint.conrelid 
					and attnum = pg_constraint.conkey[10]) key10,
				(select attname from pg_attribute where attrelid = pg_constraint.conrelid
					and attnum = pg_constraint.conkey[11]) key11,
				(select attname from pg_attribute where attrelid = pg_constraint.conrelid 
					and attnum = pg_constraint.conkey[12]) key12,
				(select attname from pg_attribute where attrelid = pg_constraint.conrelid 
					and attnum = pg_constraint.conkey[13]) key13,
				(select attname from pg_attribute where attrelid = pg_constraint.conrelid 
					and attnum = pg_constraint.conkey[14]) key14,
				(select attname from pg_attribute where attrelid = pg_constraint.conrelid 
					and attnum = pg_constraint.conkey[15]) key15,
				(select attname from pg_attribute where attrelid = pg_constraint.conrelid 
					and attnum = pg_constraint.conkey[16]) key16,
				(select attname from pg_attribute where attrelid = pg_constraint.confrelid 
					and attnum = pg_constraint.confkey[01]) fkey01,
				(select attname from pg_attribute where attrelid = pg_constraint.confrelid 
					and attnum = pg_constraint.confkey[02]) fkey02,
				(select attname from pg_attribute where attrelid = pg_constraint.confrelid 
					and attnum = pg_constraint.confkey[03]) fkey03,
				(select attname from pg_attribute where attrelid = pg_constraint.confrelid 
					and attnum = pg_constraint.confkey[04]) fkey04,
				(select attname from pg_attribute where attrelid = pg_constraint.confrelid 
					and attnum = pg_constraint.confkey[05]) fkey05,
				(select attname from pg_attribute where attrelid = pg_constraint.confrelid 
					and attnum = pg_constraint.confkey[06]) fkey06,
				(select attname from pg_attribute where attrelid = pg_constraint.confrelid 
					and attnum = pg_constraint.confkey[07]) fkey07,
				(select attname from pg_attribute where attrelid = pg_constraint.confrelid 
					and attnum = pg_constraint.confkey[08]) fkey08,
				(select attname from pg_attribute where attrelid = pg_constraint.confrelid 
					and attnum = pg_constraint.confkey[09]) fkey09,
				(select attname from pg_attribute where attrelid = pg_constraint.confrelid 
					and attnum = pg_constraint.confkey[10]) fkey10,
				(select attname from pg_attribute where attrelid = pg_constraint.confrelid 
					and attnum = pg_constraint.confkey[11]) fkey11,
				(select attname from pg_attribute where attrelid = pg_constraint.confrelid 
					and attnum = pg_constraint.confkey[12]) fkey12,
				(select attname from pg_attribute where attrelid = pg_constraint.confrelid 
					and attnum = pg_constraint.confkey[13]) fkey13,
				(select attname from pg_attribute where attrelid = pg_constraint.confrelid 
					and attnum = pg_constraint.confkey[14]) fkey14,
				(select attname from pg_attribute where attrelid = pg_constraint.confrelid 
					and attnum = pg_constraint.confkey[15]) fkey15,
				(select attname from pg_attribute where attrelid = pg_constraint.confrelid 
					and attnum = pg_constraint.confkey[16]) fkey16
			FROM	pg_constraint
				JOIN pg_class as tt ON tt.oid = pg_constraint.conrelid
					JOIN pg_namespace as ts ON tt.relnamespace = ts.oid
				JOIN pg_class as ot ON ot.oid = pg_constraint.confrelid
					JOIN pg_namespace as os ON ot.relnamespace = os.oid
			WHERE	pg_constraint.contype = 'f' ORDER BY 1";

			#endregion

			var tblDict = ((Dictionary<string, DataTable>)_params[Constants.PgTables]);
			var dt = tblDict[Constants.PgFkTable];

			NpgsqlDataAdapter da = null;

			try {
				da = new NpgsqlDataAdapter(sql, _pgConn);
				da.Fill(dt);
			}
			catch (NpgsqlException ex) {
				_log.WriteEx('E', Constants.LogTsType, ex);
			}
			finally {
				da?.Dispose();
			}
		}
	}
}
