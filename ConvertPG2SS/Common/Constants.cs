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
// <date>2015-04-02</date>
// <time>16:00</time>
//
// <summary>Program wide constants</summary>
//----------------------------------------------------------------------------------------

namespace ConvertPG2SS.Common
{
	internal static class Constants
	{
		public static string AppName => "ConvertPG2SS";
		public static char Tab => '\t';

		// Security
		public static string AesKeyFile => "aes.key";
		public static int KeySize => 32;
		public static int VectorSize => 16;

		// PostgreSQL
		public static string PgConnection => "pg_conn";
		public static string PgTables => "pg_tables";
		public static string PgSchemaTable => "pg_schematable";
		public static string PgTypeTable => "pg_typetable";
		public static string PgSeqTable => "pg_seqtable";
		public static string PgFkTable => "pg_fktable";
		public static string PgDefaultSchema => "public";
		public static int PgMaxFkeys => 16;

		// MSSQL
		public static string SsDefaultSchema => "dbo";

		// Scripts
		public static string CreateSchemas => "00_create_schemas.sql";
		public static string CreateTypes => "01_create_types_&_sequences.sql";
		public static string CreateTables => "02_create_tables.sql";
		public static string CreateBulkCopy => "03_bulk_copy.sql";
		public static string CreateIndexesEtAl => "04_create_indexes_&_constraints.sql";
		public static string CreateForeignKeys => "05_create_fk_constraints.sql";
		public static string CreateSTruncateTables => "50_truncate_tables.sql";
		public static string CreateDropTables => "51_drop_tables.sql";
		public static string CreateDropTypes => "52_drop_types_&_sequences.sql";

		// Other
		public static string TimeStamp => "yyyy'-'MM'-'dd' 'HH':'mm':'ss.ffff";
		public static string IsoDate => "yyyy-MM-dd";
		public static string IsoDateNoDelim => "yyyyMMdd";

		// Logger
		public static string LogNameDateTime => "yyyy'-'MM'-'dd'_'HH'-'mm'-'ss";
		public static long ArchiveAboveSize => 10485760L;
		public static int MaxArchiveFiles => 60;
		public static char LogTsType => 'T';
		public static char LogInfo => 'I';
		public static char LogWarning => 'W';
		public static char LogError => 'E';
		public static char Logfatal => 'I';
	}
}