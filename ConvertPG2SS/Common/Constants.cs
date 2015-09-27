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

namespace ConvertPG2SS.Common {
	internal static class Constants {
		public const string AppName = "ConvertPG2SS";
		public const char Tab = '\t';

		// Security
		public const string AesKeyFile = "aes.key";
		public const int KeySize = 32;
		public const int VectorSize = 16;

		// SQL Connections
		public const string PgConnection = "pg_conn";
		public const string PgConnKey    = "pgconnection";

		// Other
		public const string TimeStamp = "yyyy'-'MM'-'dd' 'HH':'mm':'ss.ffff";
		public const string IsoDate = "yyyy-MM-dd";
		public const string IsoDateNoDelim = "yyyyMMdd";

		// Logger
		public const string LogNameDateTime = "yyyy'-'MM'-'dd'_'HH'-'mm'-'ss";
		public const long ArchiveAboveSize = 10485760L;
		public const int MaxArchiveFiles = 60;
		public const char LogTsType  = 'T';
		public const char LogInfo    = 'I';
		public const char LogWarning = 'W';
		public const char LogError   = 'E';
		public const char Logfatal   = 'I';
	}
}