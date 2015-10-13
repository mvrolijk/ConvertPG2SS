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
// <date>2015-09-23</date>
// <time>19:40</time>
//
// <summary>Generate a .key file to use with the CryptoAes class.</summary>
//----------------------------------------------------------------------------------------

using System.IO;
using System.Security.Cryptography;

namespace GenerateKeyFile
{
	internal class Program
	{
		private const string FileName = "aes.key";

		private static void Main()
		{
			var aes = new AesManaged();

			using (var writer = new BinaryWriter(File.Open(FileName, FileMode.Create)))
			{
				writer.Write(aes.Key);
				writer.Write(aes.IV);
			}
		}
	}
}