//----------------------------------------------------------------------------------------
// <copyright company="">
//     Copyright Miguel Vrolijk (c). All rights reserved.
// </copyright>
// <author>Miguel Vrolijk</author> 
// <date>2015-09-25</date>
// <time>19:17</time>
//
// <summary>Process the Postgres db bulk conversion.</summary>
//----------------------------------------------------------------------------------------

using ConvertPG2SS.Common;
using ConvertPG2SS.Interfaces;
using Npgsql;

namespace ConvertPG2SS {
	static class ProcessBulk {
		private static IBLogger _log;
		private static IParameters _params;

		internal static void Do() {
			_log = Program.GetInstance<IBLogger>();
			_params = Program.GetInstance<IParameters>();

			var frmConn = (NpgsqlConnection)_params.Get(Constants.FrmConnection);
			
		}
	}
}
