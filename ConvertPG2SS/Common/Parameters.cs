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
// <time>16:14</time>
//
// <summary>Parameters keys</summary>
//----------------------------------------------------------------------------------------

namespace ConvertPG2SS.Common {
	internal static class Parameters {
		// Sections
		public static string PgConnSection => "pgconnection";
		public static string SsConnSection => "ssconnection";
		public static string Postgres => "postgres";
		public static string MsSql => "mssql";
		public static string Other => "other";

		// Keys PgConnection
		public static string PgConnectionServer => PgConnSection + ".server";
		public static string PgConnectionPort => PgConnSection + ".port";
		public static string PgConnectionDataBase => PgConnSection + ".database";
		public static string PgConnectionUserId => PgConnSection + ".userid";
		public static string PgConnectionPassWord => PgConnSection + ".password";
		public static string PgConnectionEncoding => PgConnSection + ".encoding";
		public static string PgConnectionSslMode => PgConnSection + ".sslmode";
		public static string PgConnectionTimeout => PgConnSection + ".timeout";

		public static string PgConnectionCommandTimeout => PgConnSection + ".commandtimeout";

		public static string PgConnectionBuffersize => PgConnSection + ".buffersize";

		// Keys SsConnection

		// Keys Postgres
		public static string PostgresIncludePublic => Postgres + ".include_public";
		public static string PostgresProcessBulk => Postgres + ".process_bulk";
		public static string PostgresUseCommit => Postgres + ".use_commit";
		public static string PostgresLimit => Postgres + ".limit";
		public static string PostgresArrayLimit => Postgres + ".array_limit";

		// Keys MSSQL
		public static string MsSqlDatabase => MsSql + ".database";
		public static string MsSqEncoding => MsSql + ".encoding";

		// Keys Other
		public static string OtherPg2Ss => Other + ".pg2ss";
		public static string OtherWorkPath => Other + ".work_path";
		public static string OtherDumpPath => Other + ".dump_path";
	}
}
