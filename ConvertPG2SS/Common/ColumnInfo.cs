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
// <date>2015-10-15</date>
// <time>08:32</time>
//
// <summary>Column info</summary>
//----------------------------------------------------------------------------------------

namespace ConvertPG2SS.Common
{
	class ColumnInfo
	{
		public static char ValueType => 'V';
		public static char SequenceType => 'S';
		public static char CheckType => 'C';

		public string Schema { get; set; }
		public string Table { get; set; }
		public string Column { get; set; }
		public string DataType { get; set; }
		public DefaultValue Default { get; set; }
		public string Comment { get; set; }

		public ColumnInfo()
		{
			Schema = "";
			Table = "";
			Column = "";
			DataType = "";
			Comment = "";
		}
	}
}
