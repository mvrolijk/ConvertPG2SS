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
// <date>2015-04-21</date>
// <time>19:37</time>
//
// <summary>This class implements various SQL data manipulation methods.</summary>
//----------------------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using ConvertPG2SS.Common;
using ConvertPG2SS.Interfaces;
using Npgsql;

namespace ConvertPG2SS.Helpers {
	internal static class Postgres {

		private static readonly IBLogger Log = Program.GetInstance<IBLogger>();
		private static readonly IParameters Params = Program.GetInstance<IParameters>();

		private static readonly Dictionary<string, DataTable> PgTables =
			(Dictionary<string, DataTable>) Params[Constants.PgTables];

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
				cmd?.Dispose();
			}
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="pgDataType"></param>
		/// <returns></returns>
		internal static string SsDataType(string pgDataType) {
			var p = pgDataType.IndexOf('[');
			var dt = p >= 0 ? pgDataType.Substring(0, p) : pgDataType;

			switch (dt) {
				case "bigint":
				case "char":
				case "character":
				case "date":
				case "dec":
				case "decimal":
				case "double precision":
				case "int":
				case "integer":
				case "money":
				case "numeric":
				case "real":
				case "smallint":
				case "text":
				case "time":
				case "xml":
					return dt;
				case "bytea":
					return "binary";
				case "boolean":
					return "bit";
				case "character varying":
					return "varchar";
				case "timestamp":
				case "timestamp without time zone":
					return "datetime2";
				case "timestamp with time zone":
					return "datetimeoffset";
				default:
					return "";
			}
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="dataType"></param>
		/// <returns></returns>
		internal static DataRow PgDomainType(string dataType) {
			var dt = PgTables[Constants.PgTypeTable];
			var critera = "type_name = '" + dataType + "'";
			var rows = dt.Select(critera);

			if (rows.Length == 1) return rows[0];
			throw new Exception(
				string.Format(
					CultureInfo.InvariantCulture,
					"{0} rows found: 1 expected.",
					rows.Length));

		}

		/// <summary>
		///     Convert a Postgres default value to SQL Server.
		/// </summary>
		/// <param name="schema"></param>
		/// <param name="def"></param>
		/// <returns></returns>
		internal static string SsDefaultValue(string schema, string def) {
			string typ;
			string val;

			// Check for sequence.
			var p = def.IndexOf("(", StringComparison.Ordinal);
			if (p > 0 && def.Substring(0, p).Equals("nextval")) {
				return Sequence(schema, def.Substring(p));
			}

			p = def.IndexOf("::", StringComparison.Ordinal);
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
						case "false":
							return "0";
						case "true":
							return "1";
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
		///     Return a sequence default string.
		/// </summary>
		/// <param name="schema"></param>
		/// <param name="def"></param>
		/// <returns></returns>
		private static string Sequence(string schema, string def) {
			var b = def.IndexOf("'", StringComparison.Ordinal);
			var e = def.IndexOf("'", b + 1, StringComparison.Ordinal);
			var seq = def.Substring(b + 1, e - b - 1);
			return "NEXT VALUE FOR [" + schema + "].[" + seq + "]";
		}

		/// <summary>
		///     Return a SS data type based on a maximum value.
		/// </summary>
		/// <param name="maxVal">Maximun value</param>
		/// <returns>SS data type</returns>
		internal static string GetTypeByMaxVal(long maxVal) {
			if (maxVal <= 255) return "tinyint";
			if (maxVal <= short.MaxValue) return "smallint";
			return maxVal <= int.MaxValue ? "int" : "bigint";
		}
	}
}