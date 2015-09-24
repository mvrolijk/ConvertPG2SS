//----------------------------------------------------------------------------------------
// <copyright company="">
//     Copyright Miguel Vrolijk (c) 2015. All rights reserved.
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

		// Security
		public const string AesKeyFile = "aes.key";
		public const int KeySize = 32;
		public const int VectorSize = 16;

		// SQL Connections
		public const string CvtConnection = "cvt_connecction";
		public const string FrmConnection = "frm_connecction";
		public const string CvtConnKey    = "cvtconnection";
		public const string FrmConnKey    = "fromconnection";

		// Other
		public const string IsoDateTime = "yyyy'-'MM'-'dd' 'HH':'mm':'ss ";
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

		// Error codes
		//public enum ErrorCodes {
		//	FolderNotFound     =   1,
		//	NoRowsFound        =   2,
		//	NoTablesSelected   =   3,
		//	NoImportsProcessed =   4,
		//	RunDateNotFound    =   5,
		//	RunDateImportFile  =   6,
		//	RecordsizeError    =   7,
		//	NullValue          =   8,
		//	RunDateError       =   9,
		//	EarlyRunDate       =  10,
		//	OtherError         = 999
		//}
	}
}