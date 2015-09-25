//----------------------------------------------------------------------------------------
// <copyright company="">
//     Copyright Miguel Vrolijk (c) 2015. All rights reserved.
// </copyright>
// <author>Miguel Vrolijk</author> 
// <date>2015-04-21</date>
// <time>15:45</time>
//
// <summary>General helper methods</summary>
//----------------------------------------------------------------------------------------

using System;
using System.IO;
using ConvertPG2SS.Common;
using ConvertPG2SS.Interfaces;

namespace ConvertPG2SS.Helpers {
	public static class General {
		/// <summary>
		///     Something went very wrong: abort.
		/// </summary>
		internal static void Abort(string msg, IBLogger log = null) {
			if (log != null) {
				log.Write('F', 'T', "Program has aborted due to unrecoverable errors.");
			}
			Environment.Exit(1);
		}


		/// <summary>
		/// 
		/// </summary>
		/// <returns></returns>
		internal static bool CheckParams(IParameters prms, IBLogger log) {
			var path = prms.Get("other.work_path").ToString();

			try {
				if (!Directory.Exists(path)) Directory.CreateDirectory(path);

				path = prms.Get("other.dump_path").ToString();
				if (!Directory.Exists(path)) Directory.CreateDirectory(path);
			}
			catch (Exception ex) {
				log.WriteEx('E', Constants.LogTsType, ex);
				return false;
			}

			return true;
		}
	}
}
