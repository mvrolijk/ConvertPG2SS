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
// <date>2015-09-23</date>
// <time>20:49</time>
//
// <summary>Process the Postgres db schema that will be converted.</summary>
//----------------------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Text;
using ConvertPG2SS.Common;
using ConvertPG2SS.Helpers;
using ConvertPG2SS.Interfaces;
using Npgsql;

namespace ConvertPG2SS {
	static class ProcessSchema {
		private static IBLogger _log;
		private static IParameters _params;

		/// <summary>
		/// 
		/// </summary>
		internal static void Do() {
			_log = Program.GetInstance<IBLogger>();
			_params = Program.GetInstance<IParameters>();

			var frmConn = (NpgsqlConnection)_params.Get(Constants.FrmConnection);
			Postgres.CreateTempAryTables(frmConn);

			#region PostgreSQL query to retrieve coloumn information from pg_catalog.
			const string sql = 
				@"SELECT n.nspname, c.relname, a.attname, a.attnum,
				(a.atttypid::regtype)::text AS regtype, a.attnotnull,
				format_type(a.atttypid, a.atttypmod) AS type,
				a.attndims,
				CASE
					WHEN a.atttypid = ANY (ARRAY[1043::oid, 1015::oid]) THEN a.atttypmod - 4
				ELSE NULL::integer
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
				d.adsrc,
				(SELECT col_description(a.attrelid, a.attnum::integer) AS col_description)
					AS comment
				FROM pg_class c
					LEFT JOIN pg_namespace n ON n.oid = c.relnamespace
					LEFT JOIN pg_tablespace t ON t.oid = c.reltablespace
					LEFT JOIN pg_attribute a ON a.attrelid = c.oid AND a.attnum > 0
					LEFT JOIN pg_attrdef d ON d.adrelid = a.attrelid AND d.adnum = a.attnum
				WHERE 
					c.relkind = 'r'::""char""
					AND n.nspname NOT IN('pg_catalog', 'information_schema', 'public')
				ORDER BY n.nspname ASC, c.relname ASC, a.attnum ASC";
#endregion
			var schemas = new List<string>();
			var path = Path.Combine(
				_params.Get("other.work_path").ToString(), 
				"create_tables.sql");

			using (var sw = new StreamWriter(path, false, Encoding.Default)) 
			using (var da = new NpgsqlDataAdapter(sql, frmConn)) {
				//sw.BuildIndexes();
				//return;
				var dt = new DataTable();
				da.Fill(dt);

				if (dt.Rows.Count == 0) return;

				sw.PrepCreateTable();

				var savedSchema = "";
				var savedTable = "";
				var defaults = new List<string[]>();

				foreach (DataRow row in dt.Rows) {
					var schema = row["nspname"].ToString();
					var table = row["relname"].ToString();

					// Schema or table changed: close table defenition.
					if (!savedSchema.Equals(schema) || !savedTable.Equals(table)) {
						if (!savedSchema.Equals(schema)) schemas.Add(schema);
						if (!string.IsNullOrEmpty(savedTable)) {
							CloseCreateTable(sw, defaults);
							defaults.Clear();
						}
						savedSchema = schema;
						savedTable = table;
						Postgres.InsertTempTable(savedSchema, savedTable, frmConn);

						sw.OpenCreateTable(schema, table);
					}
					else sw.WriteLine(",");

					// Generate column definition.
					string[] def;
					var dim = 0;

					if ((int)row["attndims"] > 0) {
						dim = Postgres.CalcArrayDim(
							row["nspname"].ToString(),
							row["relname"].ToString(),
							row["attname"].ToString(),
							frmConn);
						Postgres.InsertTempAryTableRec(
							row["nspname"].ToString(),
							row["relname"].ToString(),
							row["attname"].ToString(),
							dim,
							frmConn);
					}
					sw.GenerateColumn(row, dim, out def);
					if (!string.IsNullOrEmpty(def[0])) defaults.Add(def);
				}

				if (!string.IsNullOrEmpty(savedTable)) {
					sw.CloseCreateTable(defaults);
					sw.WriteTableDesc(frmConn);
					sw.BuildIndexes(frmConn);
					sw.WriteLine("COMMIT TRANSACTION;");
				}
				dt.Dispose();
			}

			if (schemas.Count == 0) return;

			GenCreateSchemas(schemas);
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="sw"></param>
		/// <param name="row"></param>
		/// <param name="dim"></param>
		/// <param name="def"></param>
		/// <returns></returns>
		private static void GenerateColumn(
			this TextWriter sw,
			DataRow row,
			int dim,
			out string[] def) 
		{
			var sb = new StringBuilder();
			var fmt = "";
			var ext = "";
			var dt = Postgres.SsDataType(row["regtype"].ToString());
			var tmpDef = new string[5];
			var aryDim = dim;

			if (aryDim <= 1) aryDim = 0;
			if (aryDim > 0) {
				if (aryDim > 99) fmt = "D3";
				else if (aryDim > 9) fmt = "D2";
				else fmt = "D1";
				aryDim--;
			}

			for (var i = 0; i <= aryDim; i++) {
				if (aryDim > 0) ext = (i + 1).ToString(fmt);
				if (i > 0) sw.WriteLine(",");

				sb.Append(Constants.Tab + "[" + row["attname"] + ext + "]");
				sb.Append(" [" + dt + "]");

				// Text field has a size.
				if (row["max_char_size"] != DBNull.Value) {
					sb.Append("(");
					sb.Append(((int)row["max_char_size"]).ToString().Trim());
					sb.Append(")");
				}
				// Number field has precision and scale.
				else if (row["numeric_precision"] != DBNull.Value) {
					if (dt == "numeric" || dt == "dec" || dt == "decimal") {
						sb.Append("(" + ((int)row["numeric_precision"]).ToString().Trim());
						var scale = (int)row["numeric_scale"];
						if (scale > 0) sb.Append("," + scale);
						sb.Append(")");
					}
				}

				if (((bool)row["attnotnull"])) sb.Append(" NOT NULL");
				else sb.Append(" NULL");

				if (i == 0) {
					// Store information to generate default and comment definitions later.
					if (row["adsrc"] != DBNull.Value || row["comment"] != DBNull.Value) {
						tmpDef[0] = row["nspname"].ToString();
						tmpDef[1] = row["relname"].ToString();
						tmpDef[2] = row["attname"] + ext;
						if (row["adsrc"] != DBNull.Value) tmpDef[3] = row["adsrc"].ToString();
						if (row["comment"] != DBNull.Value) tmpDef[4] = row["comment"].ToString();
					}
				}

				sw.Write(sb.ToString());
				sb.Clear();
			}

			def = tmpDef;
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="sw"></param>
		private static void PrepCreateTable(this TextWriter sw) {
			sw.WriteLine("USE " + _params.Get("mssql.database") + ";");
			sw.WriteLine("GO");
			sw.WriteLine();
			sw.WriteLine("BEGIN TRANSACTION;");
			sw.WriteLine();
			sw.WriteLine("SET ANSI_NULLS ON");
			sw.WriteLine("GO");
			sw.WriteLine("SET QUOTED_IDENTIFIER ON");
			sw.WriteLine("GO");
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="sw"></param>
		/// <param name="schema"></param>
		/// <param name="table"></param>
		private static void OpenCreateTable(
			this TextWriter sw, 
			string schema, 
			string table) 
		{
			sw.WriteLine("SET ANSI_PADDING ON");
			sw.WriteLine("GO");
			sw.WriteLine();
			sw.Write("CREATE TABLE [");
			sw.WriteLine(schema + "].[" + table + "](");
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="sw"></param>
		/// <param name="defaults"></param>
		private static void CloseCreateTable(
			this TextWriter sw,  
			IReadOnlyCollection<string[]> defaults) 
		{
			sw.WriteLine();
			sw.WriteLine(") ON [PRIMARY]");
			sw.WriteLine("GO");
			sw.WriteLine();

			if (defaults.Count == 0) return;
			sw.WriteDefaults(defaults);
			sw.WriteColumnComments(defaults);
		}

		/// <summary>
		///     Add default constraint for table. 
		/// </summary>
		/// <param name="sw"></param>
		/// <param name="defaults"></param>
		private static void WriteDefaults(this TextWriter sw, IEnumerable<string[]> defaults) {
			var first = true;

			foreach (var def in defaults) {
				if (string.IsNullOrEmpty(def[3])) continue;

				var defVal = Postgres.SsDefaultValue(def[3]);
				if (string.IsNullOrEmpty(defVal)) continue;

				if (first) {
					sw.WriteLine("SET ANSI_PADDING OFF");
					sw.WriteLine("GO");
					sw.WriteLine();
					first = false;
				}

				sw.Write("ALTER TABLE [" + def[0] + "].[");
				sw.Write(def[1] + "] ADD CONSTRAINT DF_");
				sw.Write(def[1] + "_" + def[2] + " DEFAULT ");
				sw.WriteLine("(" + defVal + ") FOR [" + def[2] + "]");
				sw.WriteLine("GO");
				sw.WriteLine();
			}
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="sw"></param>
		/// <param name="defaults"></param>
		private static void WriteColumnComments(
			this TextWriter sw,
			IEnumerable<string[]> defaults) 
		{
			foreach (var def in defaults.Where(def => !string.IsNullOrEmpty(def[4]))) {
				sw.Write("EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'");
				sw.WriteLine(def[4] + "' ,");
				sw.Write("@level0type=N'SCHEMA',@level0name=N'");
				sw.WriteLine(def[0] + "' ,");
				sw.Write("@level1type=N'TABLE',@level1name=N'");
				sw.WriteLine(def[1] + "' ,");
				sw.Write("@level2type=N'COLUMN',@level2name=N'");
				sw.WriteLine(def[2] + "'");
				sw.WriteLine("GO");
				sw.WriteLine();
			}
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="schemas"></param>
		private static void GenCreateSchemas(IEnumerable<string> schemas) {
			var path = 
				Path.Combine(_params.Get("other.work_path").ToString(), "create_schemas.sql");

			using (var sw = new StreamWriter(path, false, Encoding.Default)) {
				sw.WriteLine("USE " + _params.Get("mssql.database") + ";");
				sw.WriteLine("GO");
				sw.WriteLine();

				foreach (var schema in schemas) {
					sw.WriteLine("CREATE SCHEMA " + schema + ";");
					sw.WriteLine("GO");
					sw.WriteLine();
				}
			}
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="sw"></param>
		/// <param name="conn"></param>
		private static void WriteTableDesc(this TextWriter sw, NpgsqlConnection conn) {
			const string sql =
				@"SELECT n.nspname, c.relname, d.description
				FROM	pg_description d
						JOIN pg_class c ON d.objoid = c.oid
						JOIN pg_namespace n ON c.relnamespace = n.oid
				WHERE	d.objsubid = 0 AND c.relkind = 'r' 
						AND n.nspname NOT IN('pg_catalog', 'information_schema', 'public')
				ORDER	BY n.nspname ASC, c.relname ASC";

			using (var cmd = new NpgsqlCommand(sql, conn)) {
				using (var reader = cmd.ExecuteReader()) {
					while (reader.Read()) {
						var comment = reader["description"].ToString().Trim();
						if (string.IsNullOrEmpty(comment)) continue;

						sw.Write(
							"EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'");
						sw.WriteLine(comment + "' ,");
						sw.Write("@level0type=N'SCHEMA',@level0name=N'");
						sw.WriteLine(reader["nspname"] + "' ,");
						sw.Write("@level1type=N'TABLE',@level1name=N'");
						sw.WriteLine(reader["relname"] + "'");
						sw.WriteLine("GO");
						sw.WriteLine();
					}
				}
			}
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="sw"></param>
		/// <param name="conn"></param>
		private static void BuildIndexes(this TextWriter sw, NpgsqlConnection conn) {
			// option column values:
			// INDOPTION_DESC			0x0001 = values are in reverse order (DESC)
			// INDOPTION_NULLS_FIRST	0x0002 = NULLs are first instead of last
			const string sql =
				@"SELECT n.nspname, c.relname table_name, d.relname index_name, i.indisprimary,
						i.indisunique, a.attname, i.indoption[a.attnum - 1] as option
				FROM	pg_index i
						LEFT JOIN pg_attribute a
							  ON a.attrelid = i.indrelid
								 AND a.attnum = ANY ( i.indkey )
						LEFT JOIN pg_class c
							  ON c.oid = i.indrelid
						LEFT JOIN pg_class d
							  ON d.oid = i.indexrelid
						LEFT JOIN pg_namespace n
							  ON n.oid = c.relnamespace
				WHERE  n.nspname NOT IN('pg_toast', 'pg_catalog', 'information_schema', 'public')
				ORDER  BY n.nspname ASC, c.relname ASC, d.relname ASC, a.attnum";

			using (var cmd = new NpgsqlCommand(sql, conn)) {
				var savedSchema = "";
				var savedTable = "";
				var savedIndex = "";
				var savedType = ' ';

				using (var reader = cmd.ExecuteReader()) {
					var sb = new StringBuilder();

					// TODO: 2015-09-25: handle ASC, DESC, NULL FIRST options.
					while (reader.Read()) {
						var schema = reader["nspname"].ToString();
						var table = reader["table_name"].ToString();
						var index = reader["index_name"].ToString();

						// Schema, table or index changed: close index defenition.
						if (!savedSchema.Equals(schema) || !savedTable.Equals(table)
							|| !savedIndex.Equals(index)) 
						{
							if (sb.Length > 0) 
								sw.WriteIndex
									(savedSchema, 
									savedTable,
									savedIndex,
									savedType,
									sb.ToString());
							sb.Clear();

							savedSchema = schema;
							savedTable = table;
							savedIndex = index;

							if ((bool) reader["indisprimary"]) {
								savedType = 'P';
							}
							else if ((bool) reader["indisunique"]) {
								savedType = 'U';
							}
							else savedType = 'I';
						}
						else sb.Append(", ");

						sb.Append("[" + reader["attname"] + "]");
					}

					if (sb.Length > 0)
						sw.WriteIndex(
							savedSchema, 
							savedTable, 
							savedIndex, 
							savedType, 
							sb.ToString());
				}
			}
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="sw"></param>
		/// <param name="schema"></param>
		/// <param name="table"></param>
		/// <param name="name"></param>
		/// <param name="typ"></param>
		/// <param name="columns"></param>
		private static void WriteIndex(
			this TextWriter sw, 
			string schema, 
			string table,
			string name,
			char typ, 
			string columns) 
		{
			switch (typ) {
				case 'P':
					sw.WriteLine("GO");
					sw.WriteLine("ALTER TABLE " + schema + "." + table);
					sw.Write("ADD CONSTRAINT PK_" + table + " PRIMARY KEY CLUSTERED (");
					sw.WriteLine(columns + ");");
					break;
				case 'I':
					sw.Write("CREATE INDEX ");
					sw.WriteLine(name + " ON " + schema + "." + table);
					sw.WriteLine("(" + columns + ")");
					sw.WriteLine(
						"WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, ");
					sw.WriteLine(
						"SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ");
						sw.WriteLine("ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON)");
					break;
				default:
					throw new NotImplementedException();
			}
			
			sw.WriteLine("GO");
			sw.WriteLine();
		}
	}
}
