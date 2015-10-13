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
// <date>2015-04-22</date>
// <time>12:48</time>
//
// <summary>Parameters Interface</summary>
//----------------------------------------------------------------------------------------

using System;
using System.Collections.Generic;

namespace ConvertPG2SS.Interfaces
{
	internal interface IParameters : IDisposable
	{
		void Reload();
		object this[string key] { get; }
		bool Contains(string key);
		void WriteParametersToLog();
		IEnumerable<KeyValuePair<string, object>> GetParams();
		new void Dispose();
	}
}