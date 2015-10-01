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
// <date>2015-09-23</date>
// <time>20:49</time>
//
// <summary>Generate the SQL Server scripts from the PG schema.</summary>
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
	static class ProcessPgSchema {
		private static IBLogger _log;
		private static IParameters _params;

		/// <summary>
		///     Generate the scripts.
		/// </summary>
		internal static bool Do() {
			_log = Program.GetInstance<IBLogger>();
			_params = Program.GetInstance<IParameters>();

			var frmConn = (NpgsqlConnection) _params[Constants.PgConnection];
			Postgres.CreateTempAryTables(frmConn);
			PostgresSchemaTables.CreateTables();

			var tblDict = ((Dictionary<string, DataTable>)_params[Constants.PgTables]);
			var schemaTable = tblDict[Constants.PgSchemaTable];

			if (schemaTable.Rows.Count == 0) {
				_log.Write('E', Constants.LogTsType, "There are no records to be processed.");
				return false;
			}

			GenerateSchemaScript(schemaTable);

			var typeTable = tblDict[Constants.PgTypeTable];
			if (typeTable.Rows.Count > 0) GenerateTypeScripts(typeTable);

			GenerateTableScripts(schemaTable, frmConn);
			GenerateBuildIndexes(frmConn);

			return true;
		}

		/// <summary>
		///     Generate the CREATE SCHEMA statements.
		/// </summary>
		/// <param name="dt"></param>
		private static void GenerateSchemaScript(DataTable dt) {
			var path = Path.Combine(
				_params[Parameters.OtherWorkPath].ToString(), Constants.CreateSchemas);
			var view = new DataView(dt);
			var distinct = view.ToTable(true, "schema_name");

			using (var sw = new StreamWriter(path, false, Encoding.Default)) {
				sw.WriteUseDb();

				foreach (var row in distinct.Rows.Cast<DataRow>()
					.Where(row => !row[0].ToString().Equals(Constants.PgDefaultSchema))) 
				{
					sw.WriteLine("CREATE SCHEMA [" + row[0] + "];");
					sw.WriteLine("GO");
					sw.WriteLine();
				}
			}
		}

		/// <summary>
		///     Generate the CREATE TYPE and DROP TYPE statements.
		/// </summary>
		/// <param name="dt"></param>
		private static void GenerateTypeScripts(DataTable dt) {
			var createPath = Path.Combine(
				_params[Parameters.OtherWorkPath].ToString(), Constants.CreateTypes);
			var dropPath = Path.Combine(
				_params[Parameters.OtherWorkPath].ToString(), Constants.CreateDropTypes);

			StreamWriter swCreate = null;
			StreamWriter swDrop = null;

			try {
				swCreate = new StreamWriter(createPath, false, Encoding.Default);
				swDrop = new StreamWriter(dropPath, false, Encoding.Default);

				swCreate.WriteBeginTrans();
				swDrop.WriteBeginTrans();

				foreach (DataRow row in dt.Rows) {
					var schema = row["schema_name"].ToString().Equals(Constants.PgDefaultSchema) 
						? Constants.SsDefaultSchema 
						: row["schmea_name"].ToString();

					var typeName = "[" + schema + "].[" + row["type_name"] + "]";

					swCreate.WriteLine("CREATE TYPE " + typeName);

					var dataType = Postgres.SsDataType(row["regtype"].ToString());
					swCreate.WriteLine(
						"FROM [" + dataType + "]" + 
						GenerateColumnDimDef(row, dataType) + ";");
					swCreate.WriteLine();

					swDrop.WriteLine("DROP TYPE " + typeName + ";");
				}

				swCreate.WriteCommitTrans();
				swDrop.WriteCommitTrans();
			}
			catch (Exception ex) {
				_log.WriteEx('E', Constants.LogTsType, ex);
			}
			finally {
				swCreate?.Dispose();
				swDrop?.Dispose();
			}

		}

		/// <summary>
		///     Generate the SQL Server scripts.
		/// </summary>
		/// <param name="dt">DataTable with all the PostgreSQL schema/table/column info.</param>
		/// <param name="conn">PG connection.</param>
		private static void GenerateTableScripts(DataTable dt, NpgsqlConnection conn) {
			var createPath = Path.Combine(
				_params[Parameters.OtherWorkPath].ToString(), Constants.CreateTables);
			var dropPath = Path.Combine(
				_params[Parameters.OtherWorkPath].ToString(), Constants.CreateDropTables);
			var truncPath = Path.Combine(
				_params[Parameters.OtherWorkPath].ToString(), Constants.CreateSTruncateTables);

			StreamWriter swCreate = null;
			StreamWriter swDrop = null;
			StreamWriter swTrunc = null;

			try {
				swCreate = new StreamWriter(createPath, false, Encoding.Default);
				swDrop = new StreamWriter(dropPath, false, Encoding.Default);
				swTrunc = new StreamWriter(truncPath, false, Encoding.Default);

				swCreate.WriteBeginTrans();
				swDrop.WriteBeginTrans();
				swTrunc.WriteBeginTrans();
				swCreate.PrepCreateTable();

				var savedSchema = "";
				var savedTable = "";
				var defaults = new List<string[]>();

				foreach (DataRow row in dt.Rows) {
					var schema = row["schema_name"].ToString();
					var table = row["table_name"].ToString();

					// Schema or table changed: close table defenition.
					if (!savedSchema.Equals(schema) || !savedTable.Equals(table)) {
						if (!string.IsNullOrEmpty(savedTable)) {
							CloseCreateTable(swCreate, defaults);
							defaults.Clear();
						}
						savedSchema = schema;
						savedTable = table;
						Postgres.InsertTempTable(savedSchema, savedTable, conn);

						swCreate.OpenCreateTable(schema, table);
						swDrop.WriteDropCommand(schema, table);
						swTrunc.WriteTruncateCommand(schema, table);
					}
					else swCreate.WriteLine(",");

					// Generate column definition.
					string[] def;
					var dim = 0;

					if ((int)row["dims"] > 0) {
						dim = Postgres.CalcArrayDim(
							row["schema_name"].ToString(),
							row["table_name"].ToString(),
							row["column_name"].ToString(),
							conn);
						Postgres.InsertTempAryTableRec(
							row["schema_name"].ToString(),
							row["table_name"].ToString(),
							row["column_name"].ToString(),
							dim,
							conn);
					}
					swCreate.GenerateColumn(row, dim, out def);
					if (!string.IsNullOrEmpty(def[0])) defaults.Add(def);
				}

				if (string.IsNullOrEmpty(savedSchema)) return;

				// Complete last writes.
				swCreate.CloseCreateTable(defaults);
				swCreate.WriteTableDesc(conn);
				swCreate.WriteCommitTrans();
				swDrop.WriteCommitTrans();
				swTrunc.WriteCommitTrans();
			}
			catch (Exception ex) {
				_log.WriteEx('E', Constants.LogTsType, ex);
			}
			finally {
				swCreate?.Dispose();
				swDrop?.Dispose();
				swTrunc?.Dispose();
			}
		}

		/// <summary>
		///     Generate the column definition.
		/// </summary>
		/// <param name="tw"></param>
		/// <param name="row"></param>
		/// <param name="dim"></param>
		/// <param name="def"></param>
		private static void GenerateColumn(
			this TextWriter tw,
			DataRow row,
			int dim,
			out string[] def) 
		{
			var sb = new StringBuilder();
			var fmt = "";
			var ext = "";
			var tmpDef = new string[5];
			var aryDim = dim;

			if (aryDim <= 1) aryDim = 0;
			if (aryDim > 0) {
				if (aryDim > 99) fmt = "D3";
				else if (aryDim > 9) fmt = "D2";
				else fmt = "D1";
				aryDim--;
			}

			// Retrieve data type.
			var regType = row["regtype"].ToString();
			var dataType = Postgres.SsDataType(regType);
			if (string.IsNullOrEmpty(dataType)) dataType = regType;

			// Generate column definition.
			for (var i = 0; i <= aryDim; i++) {
				if (aryDim > 0) ext = (i + 1).ToString(fmt);
				if (i > 0) tw.WriteLine(",");

				sb.Append(Constants.Tab + "[" + row["column_name"] + ext + "]");
				sb.Append(" [" + dataType + "]");

				sb.Append(GenerateColumnDimDef(row, dataType));

				if (i == 0) {
					// Store information to generate default and comment definitions later.
					if (row["default_val"] != DBNull.Value || row["comment"] != DBNull.Value) {
						tmpDef[0] = row["schema_name"].ToString();
						tmpDef[1] = row["table_name"].ToString();
						tmpDef[2] = row["column_name"] + ext;
						if (row["default_val"] != DBNull.Value)
							tmpDef[3] = row["default_val"].ToString();
						if (row["comment"] != DBNull.Value)
							tmpDef[4] = row["comment"].ToString();
					}
				}

				tw.Write(sb.ToString());
				sb.Clear();
			}

			def = tmpDef;
		}

		/// <summary>
		///     Generate the column size definition.
		/// </summary>
		/// <param name="row"></param>
		/// <param name="dataType"></param>
		/// <returns></returns>
		private static string GenerateColumnDimDef(DataRow row, string dataType) {
			var sb = new StringBuilder();

			// Text field has a size.
			if (row["max_char_size"] != DBNull.Value) {
				sb.Append("(");
				sb.Append(((int)row["max_char_size"]).ToString().Trim());
				sb.Append(")");
			}
			// Number field has precision and scale.
			else if (row["numeric_precision"] != DBNull.Value) {
				if (dataType == "numeric" || dataType == "dec" || dataType == "decimal") {
					sb.Append("(" + ((int)row["numeric_precision"]).ToString().Trim());
					var scale = (int)row["numeric_scale"];
					if (scale > 0) sb.Append("," + scale);
					sb.Append(")");
				}
			}

			if (((bool)row["notnull"])) sb.Append(" NOT NULL");
			else sb.Append(" NULL");

			return sb.ToString();
		}

		/// <summary>
		///     Write required SQL Server commands before starting to write the 
		///     CREATE TABLE statements. 
		/// </summary>
		/// <param name="tw"></param>
		private static void PrepCreateTable(this TextWriter tw) {
			tw.WriteLine("SET ANSI_NULLS ON");
			tw.WriteLine("GO");
			tw.WriteLine("SET QUOTED_IDENTIFIER ON");
			tw.WriteLine("GO");
		}

		/// <summary>
		///     Write the starting CREATE TABLE lines.
		/// </summary>
		/// <param name="tw"></param>
		/// <param name="schema"></param>
		/// <param name="table"></param>
		private static void OpenCreateTable(
			this TextWriter tw, 
			string schema, 
			string table) 
		{
			tw.WriteLine("SET ANSI_PADDING ON");
			tw.WriteLine("GO");
			tw.WriteLine();
			tw.Write("CREATE TABLE [");
			tw.WriteLine(schema + "].[" + table + "](");
		}

		/// <summary>
		///     Write the CREATE TABLE closing.
		/// </summary>
		/// <param name="tw"></param>
		/// <param name="defaults"></param>
		private static void CloseCreateTable(
			this TextWriter tw,  
			IReadOnlyCollection<string[]> defaults) 
		{
			tw.WriteLine();
			tw.WriteLine(") ON [PRIMARY]");
			tw.WriteLine("GO");
			tw.WriteLine();

			if (defaults.Count == 0) return;
			tw.WriteDefaults(defaults);
			tw.WriteColumnComments(defaults);
		}


		/// <summary>
		///     Write the DROP TABLE statement.
		/// </summary>
		/// <param name="tw"></param>
		/// <param name="schema"></param>
		/// <param name="table"></param>
		private static void WriteDropCommand(this TextWriter tw, string schema, string table) {
			// TODO: 2015-09-27: the issue of constraints such as FOREIGN KEYS will come up.
			var qual = "[" + schema + "].[" + table + "]";
			tw.Write("IF OBJECT_ID ('" + qual + "') ");
			tw.WriteLine("IS NOT NULL DROP TABLE " + qual + ";");
		}

		/// <summary>
		///     Write the TRUNCATE TABLE statement.
		/// </summary>
		/// <param name="tw"></param>
		/// <param name="schema"></param>
		/// <param name="table"></param>
		private static void WriteTruncateCommand(this TextWriter tw, string schema, string table) {
			// TODO: 2015-09-27: the issue of constraints such as FOREIGN KEYS will come up.
			var qual = "[" + schema + "].[" + table + "]";
			tw.WriteLine("TRUNCATE TABLE " + qual+ ";");
		}

		/// <summary>
		///     Add default constraints for table. 
		/// </summary>
		/// <param name="tw"></param>
		/// <param name="defaults"></param>
		private static void WriteDefaults(this TextWriter tw, IEnumerable<string[]> defaults) {
			var first = true;

			foreach (var def in defaults) {
				if (string.IsNullOrEmpty(def[3])) continue;

				var defVal = Postgres.SsDefaultValue(def[3]);
				if (string.IsNullOrEmpty(defVal)) continue;

				if (first) {
					tw.WriteLine("SET ANSI_PADDING OFF");
					tw.WriteLine("GO");
					tw.WriteLine();
					first = false;
				}

				tw.Write("ALTER TABLE [" + def[0] + "].[");
				tw.Write(def[1] + "] ADD CONSTRAINT DF_");
				tw.Write(def[1] + "_" + def[2] + " DEFAULT ");
				tw.WriteLine("(" + defVal + ") FOR [" + def[2] + "]");
				tw.WriteLine("GO");
				tw.WriteLine();
			}
		}

		/// <summary>
		///     Write the column comments.
		/// </summary>
		/// <param name="tw"></param>
		/// <param name="defaults"></param>
		private static void WriteColumnComments(
			this TextWriter tw,
			IEnumerable<string[]> defaults) 
		{
			foreach (var def in defaults.Where(def => !string.IsNullOrEmpty(def[4]))) {
				tw.Write("EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'");
				tw.WriteLine(def[4] + "' ,");
				tw.Write("@level0type=N'SCHEMA',@level0name=N'");
				tw.WriteLine(def[0] + "' ,");
				tw.Write("@level1type=N'TABLE',@level1name=N'");
				tw.WriteLine(def[1] + "' ,");
				tw.Write("@level2type=N'COLUMN',@level2name=N'");
				tw.WriteLine(def[2] + "'");
				tw.WriteLine("GO");
				tw.WriteLine();
			}
		}

		/// <summary>
		///     Write the table descriptions.
		/// </summary>
		/// <param name="tw"></param>
		/// <param name="conn"></param>
		private static void WriteTableDesc(this TextWriter tw, NpgsqlConnection conn) {
			const string sql =
				@"SELECT n.nspname AS schema_name, c.relname AS table_name, d.description
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

						tw.Write(
							"EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'");
						tw.WriteLine(comment + "' ,");
						tw.Write("@level0type=N'SCHEMA',@level0name=N'");
						tw.WriteLine(reader["schema_name"] + "' ,");
						tw.Write("@level1type=N'TABLE',@level1name=N'");
						tw.WriteLine(reader["table_name"] + "'");
						tw.WriteLine("GO");
						tw.WriteLine();
					}
				}
			}
		}

		/// <summary>
		///     Write the different create indeces/key constraint statements. 
		/// </summary>
		/// <param name="conn"></param>
		private static void GenerateBuildIndexes(NpgsqlConnection conn) {
			var indexPath = Path.Combine(
				_params[Parameters.OtherWorkPath].ToString(), Constants.CreateIndexesEtAl);

			// option column values:
			// INDOPTION_DESC			0x0001 = values are in reverse order (DESC)
			// INDOPTION_NULLS_FIRST	0x0002 = NULLs are first instead of last
			const string sql =
				@"SELECT n.nspname AS schema_name, c.relname AS table_name, 
						d.relname AS index_name, i.indisprimary,
						i.indisunique, a.attname AS column_name, 
						i.indoption[a.attnum - 1] as option
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

			using (var cmd = new NpgsqlCommand(sql, conn)) 
			using (var sw = new StreamWriter(indexPath, false, Encoding.Default)) {
				sw.WriteBeginTrans();

				var savedSchema = "";
				var savedTable = "";
				var savedIndex = "";
				var savedType = ' ';

				using (var reader = cmd.ExecuteReader()) {
					var sb = new StringBuilder();

					// TODO: 2015-09-25: handle ASC, DESC, NULL FIRST options.
					while (reader.Read()) {
						var schema = reader["schema_name"].ToString();
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

						sb.Append("[" + reader["column_name"] + "]");
					}

					if (sb.Length > 0)
						sw.WriteIndex(
							savedSchema, 
							savedTable, 
							savedIndex, 
							savedType, 
							sb.ToString());

					sw.WriteCommitTrans();
				}
			}
		}

		/// <summary>
		///     Write the index / constraint statement.
		/// </summary>
		/// <param name="tw"></param>
		/// <param name="schema"></param>
		/// <param name="table"></param>
		/// <param name="name"></param>
		/// <param name="typ"></param>
		/// <param name="columns"></param>
		private static void WriteIndex(
			this TextWriter tw, 
			string schema, 
			string table,
			string name,
			char typ, 
			string columns) 
		{
			var qualTable = "[" + schema + "].[" + table + "]";

			switch (typ) {
				case 'P':
					tw.WriteLine("ALTER TABLE " + qualTable);
					tw.Write("ADD CONSTRAINT PK_" + table + " PRIMARY KEY CLUSTERED (");
					tw.WriteLine(columns + ");");
					break;
				case 'I':
					tw.Write("CREATE INDEX ");
					tw.WriteLine(name + " ON " + qualTable);
					tw.WriteLine("(" + columns + ")");
					tw.WriteLine(
						"WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, ");
					tw.WriteLine(
						"SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ");
						tw.WriteLine("ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON)");
					break;
				case 'U':
					tw.WriteLine("ALTER TABLE " + qualTable);
					tw.WriteLine("ADD CONSTRAINT UK_" + table + " UNIQUE (" + columns + ");");
					break;
				default:
					throw new NotImplementedException();
			}
			
			tw.WriteLine("GO");
			tw.WriteLine();
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="tw"></param>
		private static void WriteUseDb(this TextWriter tw) {
			tw.WriteLine("USE " + _params[Parameters.MsSqlDatabase] + ";");
			tw.WriteLine("GO");
			tw.WriteLine();
		}
		/// <summary>
		///     Write begin transaction.
		/// </summary>
		/// <param name="tw"></param>
		private static void WriteBeginTrans(this TextWriter tw) {
			tw.WriteUseDb();
			tw.WriteLine("BEGIN TRANSACTION;");
			tw.WriteLine();
		}

		/// <summary>
		///     Write commit transaction.
		/// </summary>
		/// <param name="tw"></param>
		private static void WriteCommitTrans(this TextWriter tw) {
			tw.WriteLine();
			tw.WriteLine("COMMIT TRANSACTION;");
		}
	}
}
