//----------------------------------------------------------------------------------------
// <copyright company="">
//     Copyright Miguel Vrolijk (c) 2015. All rights reserved.
// </copyright>
// <author>Miguel Vrolijk</author> 
// <date>2015-04-22</date>
// <time>12:48</time>
//
// <summary>Parameters Interface</summary>
//----------------------------------------------------------------------------------------

using System;
using System.Collections.Generic;

namespace ConvertPG2SS.Interfaces {
	internal interface IParameters : IDisposable {
		void Reload();
		object Get(string key);
		bool Contains(string key);
		void WriteParametersToLog();
		IEnumerable<KeyValuePair<string, object>> GetParams();
		new void Dispose();
	}
}