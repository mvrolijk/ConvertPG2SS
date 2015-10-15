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
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using ConvertPG2SS.Common;
using ConvertPG2SS.Interfaces;

namespace ConvertPG2SS.Helpers
{
	public static class General
	{
		private const string Cast = "::";
		private const string Array = "ANY (ARRAY[";

		/// <summary>
		///     Something went very wrong: abort.
		/// </summary>
		internal static void Abort(string msg, IBLogger log = null)
		{
			log?.Write('F', 'T', "Program has aborted due to unrecoverable errors.");
			Environment.Exit(1);
		}


		/// <summary>
		/// 
		/// </summary>
		/// <returns></returns>
		internal static bool CheckParams(IParameters prms, IBLogger log)
		{
			var path = prms[Parameters.OtherWorkPath].ToString();

			try
			{
				if (!Directory.Exists(path)) Directory.CreateDirectory(path);

				path = prms[Parameters.OtherDumpPath].ToString();
				if (!Directory.Exists(path)) Directory.CreateDirectory(path);
			}
			catch (Exception ex)
			{
				log.WriteEx('E', Constants.LogTsType, ex);
				return false;
			}

			return true;
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="str"></param>
		/// <returns></returns>
		internal static string SanitizeString(string str)
		{
			if (string.IsNullOrEmpty(str)) return str;

			var sb = new StringBuilder();

			for (var i = 0; i < str.Length; i++)
			{
				var ch = str[i];

				if (ch == '\'')
				{
					var ch2 = i < str.Length - 1 ? str[i + 1] : ' ';
					if (ch2 != '\'') sb.Append("\'\'");
					continue;
				}
				sb.Append(ch);
			}

			return sb.ToString();
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="str"></param>
		/// <returns></returns>
		internal static string SanitizePgString(string str)
		{
			if (string.IsNullOrEmpty(str)) return str;

			var sb = new StringBuilder();

			var tokens = new Dictionary<int, char>();

			// Check for casts
			var p = 0;
			while (true)
			{
				p = p == 0 
					? str.IndexOf(Cast, StringComparison.Ordinal) 
					: str.IndexOf(Cast, p + Cast.Length, StringComparison.Ordinal);

				if (p > 0) tokens.Add(p, 'C');
				else break;
			}

			// Check for arrays
			p = 0;
			while (true)
			{
				p = p == 0
					? str.IndexOf(Array, StringComparison.Ordinal)
					: str.IndexOf(Array, p + Array.Length, StringComparison.Ordinal);

				if (p > 0) tokens.Add(p, 'A');
				else break;
			}

			var query = from t in tokens orderby t.Key select t;
			var ary = query.ToArray();

			p = 0;
			var arrayFlag = false;
			for (var i = 0; i < ary.Count(); i++)
			{
				var kvp = ary[i];

				if (kvp.Value == 'A')
				{
					sb.Append(" IN ((");
					p = kvp.Key + Array.Length + 1;
					arrayFlag = true;
				}
				else
				{
					sb.Append(str.Substring(p, kvp.Key - p));
					p = kvp.Key + Cast.Length;

					var kvpAhead = i < ary.Length - 1 
						? ary[i + 1] 
						: new KeyValuePair<int, char>(-1, ' ');

					if (p > kvpAhead.Key)
					{
						p = kvpAhead.Key;
						continue;
					}

					while (true)
					{
						var ch = str[p];

						if (ch == ',' || ch == ')') break;
						if (ch == ']' || (!arrayFlag && ch == '=')) break;

						p++;
						if (kvpAhead.Key != -1 && p == kvpAhead.Key) break;
						if (p >= str.Length) break;
					}
				}
			}
			if (arrayFlag) sb.Append("))");

			var s = sb.ToString();
			return sb.ToString();
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="ba"></param>
		/// <returns></returns>
		internal static string ConvertBinToHex(byte[] ba)
		{
			var tempStr = new StringBuilder(ba.Length*2 + 2);
			//tempStr.Append("0x");
			foreach (var b in ba) tempStr.AppendFormat("{0:X2}", b);
			return tempStr.ToString();
		}
	}
}