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
			var path = prms["other.work_path"].ToString();

			try {
				if (!Directory.Exists(path)) Directory.CreateDirectory(path);

				path = prms["other.dump_path"].ToString();
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
