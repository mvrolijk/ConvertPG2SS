//----------------------------------------------------------------------------------------
// <copyright company="">
//     Copyright Miguel Vrolijk (c) 2015. All rights reserved.
// </copyright>
// <author>Miguel Vrolijk</author> 
// <date>2015-04-21</date>
// <time>19:37</time>
//
// <summary>This partial class implements various SQL data manipulation methods.</summary>
//----------------------------------------------------------------------------------------

using System;
using System.Data;
using System.Globalization;
using ConvertPG2SS.Common;
using ConvertPG2SS.Interfaces;
using Npgsql;

namespace ConvertPG2SS.Helpers {
	internal static class Postgres {

		//private static readonly IBLogger Log = new BLoggerService();
		private static readonly IBLogger Log = Program.GetInstance<IBLogger>();

		/// <summary>
		///     Executes a SQL statement.
		///     WARNING: only use for program-generated SQL statements. Statements
		///     which include user input should be executed using prepared
		///     statements to avoid possible injection attacks.
		/// </summary>
		/// <param name="sqlStmt">The SQL statement to execute</param>
		/// <param name="conn">The open connection object to the database</param>
		internal static int ExecuteSql(string sqlStmt, NpgsqlConnection conn) {
			NpgsqlCommand cmd = null;

			try {
				cmd = new NpgsqlCommand(sqlStmt, conn);
				return cmd.ExecuteNonQuery();
			}
			catch (NpgsqlException ex) {
				Log.Write('E', ' ', sqlStmt);
				Log.WriteEx('E', Constants.LogTsType, ex);
				return -1;
			}
			finally {
				if (cmd != null) cmd.Dispose();
			}
		}

		/// <summary>
		///     Get the record count from 'table' with selection 'select'. This
		///     method is similar to the handy Access VBA function DCount.
		/// </summary>
		/// <param name="table">Name of the table</param>
		/// <param name="select">The WHERE clause</param>
		/// <param name="conn">The open connection object to the database</param>
		/// <returns>The record count</returns>
		internal static long GetRecordCount(
			string table, 
			string select,
			NpgsqlConnection conn) 
		{
			string sql;
			if (@select.Length > 0) {
				sql = String.Format(
					CultureInfo.InvariantCulture,
					"SELECT COUNT(*) FROM {0} WHERE {1}",
					table, @select);
			}
			else {
				sql = String.Format(
					CultureInfo.InvariantCulture,
					"SELECT COUNT(*) FROM {0}",
					table);
			}

			NpgsqlCommand cmd = null;
			try {
				cmd = new NpgsqlCommand(sql, conn);
				return (long) cmd.ExecuteScalar();
			}
			catch (NpgsqlException ex) {
				Log.Write('E', ' ', "Table.: " + table);
				Log.Write('E', ' ', "Select: " + @select);
				Log.WriteEx('E', Constants.LogTsType, ex);
			}
			finally {
				if (cmd != null) cmd.Dispose();
			}
			return -1;
		}

		/// <summary>
		///     Gets the first value of 'field' from 'table' using selection
		///     'select'. This method is similar to Access VBA DLookUp function.
		/// </summary>
		/// <param name="field">Name of field value you want to retrieve</param>
		/// <param name="table">The table that contains the field</param>
		/// <param name="select">The WHERE clause</param>
		/// <param name="conn">The open connection object to the database</param>
		/// <returns>The field value, or null if an error occured</returns>
		internal static object GetScalar(
			string field,
			string table,
			string select,
			NpgsqlConnection conn) 
		{
			string sql;
			if (@select.Length > 0) {
				sql = String.Format(
					CultureInfo.InvariantCulture,
					"SELECT {0} FROM {1} WHERE {2} LIMIT 1",
					field,
					table,
					@select);
			}
			else {
				sql = String.Format(
					CultureInfo.InvariantCulture,
					"SELECT {0} FROM {1} LIMIT 1",
					field,
					table);
			}

			NpgsqlCommand cmd = null;
			try {
				cmd = new NpgsqlCommand(sql, conn);
				var t = cmd.ExecuteScalar();
				return t;
			}
			catch (NpgsqlException ex) {
				Log.Write('E', ' ', "Field.: " + field);
				Log.Write('E', ' ', "Table.: " + table);
				Log.Write('E', ' ', "Select: " + @select);
				Log.WriteEx('E', Constants.LogTsType, ex);
				return new object();
			}
			finally {
				if (cmd != null) cmd.Dispose();
			}
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="schema"></param>
		/// <param name="table"></param>
		/// <param name="column"></param>
		/// <param name="conn"></param>
		/// <returns></returns>
		internal static int CalcArrayDim(
			string schema, 
			string table, 
			string column, 
			NpgsqlConnection conn) 
		{
			var dim = GetScalar(
				"cardinality(" + column + ")",
				schema + "." + table,
				"",
				conn);

			//return (int) dim;
			return dim == null || dim == DBNull.Value ? 0 : (int)dim;
		}

		internal static bool CheckConnection(NpgsqlConnection conn, IBLogger log) {
			if (conn == null) return false;

			if (conn.State != ConnectionState.Open) return false;

			// Try a simple command to confirm that the SQL connection is
			// indeed working.
			var cmd = new NpgsqlCommand("select 1", conn);
			int test;
			try {
				test = (int)cmd.ExecuteScalar();
			}
			catch (NpgsqlException ex) {
				log.WriteEx('E', Constants.LogTsType, ex);
				test = 0;
			}
			finally {
				cmd.Dispose();
			}
			return test == 1;
		}

		internal static string SsDataType(string pgDataType) {
			var p = pgDataType.IndexOf('[');
			var dt = p >= 0 ? pgDataType.Substring(0, p) : pgDataType;

			switch (dt) {
				case "bigint":
					return "bigint";
				case "bytea":
					return "binary";
				case "boolean":
					return "bit";
				case "char":
					return "char";
				case "character":
					return "character";
				case "character varying":
					return "varchar";
				case "date":
					return "date";
				case "timestamp":
				case "timestamp without time zone":
					return "datetime2";
				case "timestamp with time zone":
					return "datetimeoffset";
				case "dec":
					return "dec";
				case "decimal":
					return "decimal";
				case "double precision":
					return "double precision";
				case "int":
					return "int";
				case "integer":
					return "integer";
				case "money":
					return "money";
				case "numeric":
					return "numeric";
				case "real":
					return "real";
				case "smallint":
					return "smallint";
				case "text":
					return "text";
				case "time":
					return "time";
				case "xml":
					return "xml";
				default:
					throw new NotImplementedException();
			}
		}

		internal static string SsDefaultValue(string def) {
			var p = def.IndexOf("::", StringComparison.Ordinal);
			string typ;
			string val;

			if (p > 0) {
				typ = def.Substring(p + 2);
				val = def.Substring(0, p);
			}
			else {
				typ = "";
				val = def;
			}

			switch (typ) {
				case "":
					switch (val) {
						case "now()":
							return "GETDATE()";
						default:
							return val;
					}
				case "bpchar":
				case "date":
					return val;
				default:
					return null;
			}
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="conn"></param>
		internal static void CreateTempAryTables(NpgsqlConnection conn) {
			var sql =
				@"DROP TABLE IF EXISTS array_dimensions;
				CREATE TABLE array_dimensions (
					schema_name text NOT NULL,
					table_name text NOT NULL,
					column_name text NOT NULL,
					dim integer,
					CONSTRAINT array_dimensions_pkey 
						PRIMARY KEY (schema_name, table_name, column_name)
				)";
			ExecuteSql(sql, conn);

			sql =
				@"DROP TABLE IF EXISTS tables;
				CREATE TABLE tables (
					schema_name text NOT NULL,
					table_name text NOT NULL,
					CONSTRAINT tables_pkey 
						PRIMARY KEY (schema_name, table_name)
				)";

			ExecuteSql(sql, conn);
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="schema"></param>
		/// <param name="table"></param>
		/// <param name="column"></param>
		/// <param name="dim"></param>
		/// <param name="conn"></param>
		internal static void InsertTempAryTableRec(
			string schema,
			string table,
			string column,
			int dim,
			NpgsqlConnection conn) 
		{
			var sql = string.Format(
				CultureInfo.InvariantCulture,
				"INSERT INTO array_dimensions (" +
				"schema_name, table_name, column_name, dim) " +
				"VALUES ('{0}', '{1}', '{2}', {3})",
				schema, table, column, dim);
			ExecuteSql(sql, conn);
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="schema"></param>
		/// <param name="table"></param>
		/// <param name="conn"></param>
		internal static void InsertTempTable(
			string schema,
			string table,
			NpgsqlConnection conn) 
		{
			var sql = string.Format(
				CultureInfo.InvariantCulture,
				"INSERT INTO tables (" +
				"schema_name, table_name) " +
				"VALUES ('{0}', '{1}')",
				schema, table);
			ExecuteSql(sql, conn);
		}
	}
}