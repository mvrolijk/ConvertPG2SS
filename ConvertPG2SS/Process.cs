//----------------------------------------------------------------------------------------
// <copyright company="">
//     Copyright Miguel Vrolijk (c). All rights reserved.
// </copyright>
// <author>Miguel Vrolijk</author> 
// <date>2015-09-23</date>
// <time>20:49</time>
//
// <summary>Process the Postgres db that will be converted.</summary>
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
	static class Process {
		private static IBLogger _log;
		private static IParameters _params;
		private const char Tab = '\t';

		/// <summary>
		/// 
		/// </summary>
		internal static void Do() {
			_log = Program.GetInstance<IBLogger>();
			_params = Program.GetInstance<IParameters>();

			if (!CheckPaths()) return;

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
					AND n.nspname NOT IN('pg_catalog', 'information_schema')
				ORDER BY n.nspname ASC, c.relname ASC, a.attnum ASC";
#endregion
			var frmConn = (NpgsqlConnection) _params.Get(Constants.FrmConnection);
			var schemas = new List<string>();
			var path = Path.Combine(
				_params.Get("other.work_path").ToString(), 
				"create_tables.sql");

			using (var sw = new StreamWriter(path, false, Encoding.Default)) 
			using (var cmd = new NpgsqlCommand(sql, frmConn)) {
				sw.PrepCreateTable();

				var savedSchema = "";
				var savedTable = "";
				var defaults = new List<string[]>();

				using (var reader = cmd.ExecuteReader()) {
					while (reader.Read()) {
						var schema = reader["nspname"].ToString();
						var table = reader["relname"].ToString();

						// Schema or table changed: close table defenition.
						if (!savedSchema.Equals(schema) || !savedTable.Equals(table)) {
							if (!savedSchema.Equals(schema)) schemas.Add(schema);
							if (!string.IsNullOrEmpty(savedTable)) {
								CloseCreateTable(sw, defaults);
								defaults.Clear();
							}
							savedSchema = schema;
							savedTable = table;

							sw.OpenCreateTable(schema, table);
						}
						else sw.WriteLine(",");

						// Generate column definition.
						string[] def;
						sw.Write(GenerateColumn(reader, out def));
						if (!string.IsNullOrEmpty(def[0])) defaults.Add(def);
					}
				}
				if (!string.IsNullOrEmpty(savedTable)) {
					sw.CloseCreateTable(defaults);
					sw.WriteTableDesc();
					sw.WriteLine("COMMIT TRANSACTION;");
				}
			}

			if (schemas.Count == 0) return;

			GenCreateSchemas(schemas);
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="reader"></param>
		/// <param name="def"></param>
		/// <returns></returns>
		private static string GenerateColumn(IDataRecord reader, out string[] def) {
			var sb = new StringBuilder();

			var dt = Postgres.SsDataType(reader["regtype"].ToString());

			sb.Append(Tab + "[" + reader["attname"]  + "]");
			sb.Append(" [" + dt + "]");

			// Text field has a size.
			if (reader["max_char_size"] != DBNull.Value) {
				sb.Append("(");
				sb.Append(((int)reader["max_char_size"]).ToString().Trim());
				sb.Append(")");
			}
			// Number field has precision and scale.
			else if (reader["numeric_precision"] != DBNull.Value) {
				if (dt == "numeric" || dt == "dec" || dt == "decimal") {
					sb.Append("(" + ((int) reader["numeric_precision"]).ToString().Trim());
					var scale = (int) reader["numeric_scale"];
					if (scale > 0) sb.Append("," + scale);
					sb.Append(")");
				}
			}

			if (((bool)reader["attnotnull"])) sb.Append(" NOT NULL");
			else sb.Append(" NULL");

			// Store information to generate default and comment definitions later.
			var tmpDef = new string[5];
			if (reader["adsrc"] != DBNull.Value || reader["comment"] != DBNull.Value) {
				tmpDef[0] = reader["nspname"].ToString();
				tmpDef[1] = reader["relname"].ToString();
				tmpDef[2] = reader["attname"].ToString();
				if (reader["adsrc"] != DBNull.Value) tmpDef[3] = reader["adsrc"].ToString();
				if (reader["comment"] != DBNull.Value) tmpDef[4] = reader["comment"].ToString();
			}

			def = tmpDef;
			return sb.ToString();
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="sw"></param>
		private static void PrepCreateTable(this StreamWriter sw) {
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
			this StreamWriter sw, 
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
			this StreamWriter sw,  
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
		/// 
		/// </summary>
		/// <param name="sw"></param>
		/// <param name="defaults"></param>
		private static void WriteDefaults(this StreamWriter sw, IEnumerable<string[]> defaults) {
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
				sw.Write(def[1] + "] ADD DEFAULT ");
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
			this StreamWriter sw,
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
		private static void WriteTableDesc(this StreamWriter sw) {
			var frmConn = (NpgsqlConnection)_params.Get(Constants.FrmConnection);

			const string sql =
				@"SELECT n.nspname, c.relname, d.description
				FROM	pg_description d
						JOIN pg_class c ON d.objoid = c.oid
						JOIN pg_namespace n ON c.relnamespace = n.oid
				WHERE	d.objsubid = 0 AND c.relkind = 'r' 
						AND n.nspname NOT IN('pg_catalog', 'information_schema')
				ORDER	BY n.nspname ASC, c.relname ASC";

			using (var cmd = new NpgsqlCommand(sql, frmConn)) {
				using (var reader = cmd.ExecuteReader()) {
					while (reader.Read()) {
						var comment = reader["description"].ToString().Trim();
						if (string.IsNullOrEmpty(comment)) continue;

						sw.Write("EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'");
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
		/// <returns></returns>
		private static bool CheckPaths() {
			var path = _params.Get("other.work_path").ToString();

			try {
				if (!Directory.Exists(path)) Directory.CreateDirectory(path);

				path = _params.Get("other.dump_path").ToString();
				if (!Directory.Exists(path)) Directory.CreateDirectory(path);
			}
			catch (Exception ex) {
				_log.WriteEx('E', Constants.LogTsType, ex);
				return false;
			}

			return true;
		}
	}
}
