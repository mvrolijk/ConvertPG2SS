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

namespace ConvertPG2SS.Helpers
{
	internal static class Postgres
	{
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
		internal static int ExecuteSql(string sqlStmt, NpgsqlConnection conn)
		{
			NpgsqlCommand cmd = null;

			try
			{
				cmd = new NpgsqlCommand(sqlStmt, conn);
				return cmd.ExecuteNonQuery();
			}
			catch (NpgsqlException ex)
			{
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
		internal static string SsDataType(string pgDataType)
		{
			var p = pgDataType.IndexOf('[');
			var dt = p >= 0 ? pgDataType.Substring(0, p) : pgDataType;

			switch (dt)
			{
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
		internal static DataRow PgDomainType(string dataType)
		{
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
		internal static DefaultValue SsDefaultValue(string schema, string def)
		{
			string typ;
			string val;

			// Check for sequence.
			var p = def.IndexOf("(", StringComparison.Ordinal);
			if (p > 0 && def.Substring(0, p).Equals("nextval")) {
				return Sequence(schema, def.Substring(p));
			}

			p = def.IndexOf("::", StringComparison.Ordinal);
			if (p > 0)
			{
				var p2 = def.IndexOf("(", p + 2, StringComparison.Ordinal);
				typ = p2 > 0 ? def.Substring(p + 2, p2 - p - 2) : def.Substring(p + 2);
				val = def.Substring(0, p);
			}
			else
			{
				typ = "";
				val = def;
			}

			var defType = new DefaultValue {Type = DefaultValue.ValueType};

			switch (typ)
			{
				case "":
					switch (val)
					{
						case "now()":
							defType.Value = "GETDATE()";
							break;
						case "false":
							defType.Value = "0";
							break;
						case "true":
							defType.Value = "1";
							break;
						default:
							defType.Value = val;
							break;
					}
					break;
				case "bpchar":
				case "date":
				case "timestamp without time zone":
				case "character":
				case "character varying":
				case "interval":
				case "text":
					defType.Value = val;
					break;
				default:
					defType.Value = "";
					break;
			}

			return defType;
		}

		/// <summary>
		///     Return a sequence default string.
		/// </summary>
		/// <param name="schema"></param>
		/// <param name="def"></param>
		/// <returns></returns>
		private static DefaultValue Sequence(string schema, string def)
		{
			var b = def.IndexOf("'", StringComparison.Ordinal);
			var e = def.IndexOf("'", b + 1, StringComparison.Ordinal);
			var seq = def.Substring(b + 1, e - b - 1);

			var defType = new DefaultValue
			{
				Type = DefaultValue.SequenceType,
				Value = "NEXT VALUE FOR [" + schema + "].[" + seq + "]",
				Sequence = seq
				
			};

			return defType;
		}

		/// <summary>
		///     Return a maximum value depending on the SS data type.
		/// </summary>
		/// <returns>Maximum value</returns>
		internal static long GetMaxValByType(string dataType)
		{
			switch (dataType)
			{
				case "smallint":
					return short.MaxValue;
				case "integer":
					return int.MaxValue;
				case "bigint":
					return long.MaxValue;
			}
			return -1;
		}
	}
}