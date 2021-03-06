﻿//----------------------------------------------------------------------------------------
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
// <summary>The program uses this class to set and retrieve the parameters it needs to
// function. This class is instatiated as a sigleton by SimpleInjector</summary>
//----------------------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using ConvertPG2SS.Common;
using ConvertPG2SS.Helpers;
using ConvertPG2SS.Interfaces;
using IniParser;
using IniParser.Exceptions;
using IniParser.Model;
using Npgsql;

namespace ConvertPG2SS.Services
{
	/// <summary>
	///     This module loads and returns various parameters.
	/// </summary>
	internal class ParametersService : IParameters
	{
		private readonly IBLogger _log = new BLoggerService();
		private readonly byte[] _key = new byte[Constants.KeySize];
		private readonly byte[] _vector = new byte[Constants.VectorSize];

		// Flag: Has Dispose already been called?
		private bool _disposed;

		/// <summary>
		///     The parameters dictionary.
		/// </summary>
		private readonly Dictionary<string, object>
			_param = new Dictionary<string, object>();

		public ParametersService()
		{
			LoadAesKey();
			Reload();
		}

		/// <summary>
		///     Reload the parameters.
		/// </summary>
		public void Reload()
		{
			if (_param.Count > 0) DisposeMe();
			_param.Clear();
			ProcessIniFile();

			var pgConn = ConnectToPgDb(Parameters.PgConnSection);

			if (pgConn == null) {
				General.Abort("Connection to one or more SQL databases failed.");
			}

			_param.Add(Constants.PgConnection, pgConn);

			// Add tables.
			var tblDict = new Dictionary<string, DataTable>
			{
				{Constants.PgSchemaTable, new DataTable(Constants.PgSchemaTable)},
				{Constants.PgTypeTable, new DataTable(Constants.PgTypeTable)},
				{Constants.PgSeqTable, new DataTable(Constants.PgSeqTable)},
				{Constants.PgFkTable, new DataTable(Constants.PgFkTable)},
				{Constants.PgCheckTable, new DataTable(Constants.PgCheckTable)}
			};
			_param.Add(Constants.PgTables, tblDict);
		}


		/// <summary>
		///    Indexer.
		/// </summary>
		/// <param name="key"></param>
		/// <returns></returns>
		public object this[string key]
		{
			get
			{
				if (_param.ContainsKey(key)) return _param[key];
				var msg = "Parameter '" + key + "' does not exist.";
				_log.Write('E', Constants.LogTsType, msg);
				throw new ArgumentException(msg);
			}
		}

		/// <summary>
		///     Returns true if 'key' exists, false otherwise.
		/// </summary>
		/// <param name="key">The key to check</param>
		/// <returns>True if key exists, false otherwise</returns>
		public bool Contains(string key)
		{
			return _param.ContainsKey(key);
		}

		/// <summary>
		///     Return a collection with all the parameters.
		/// </summary>
		/// <returns>The parameters' collection</returns>
		public IEnumerable<KeyValuePair<string, object>> GetParams()
		{
			return _param;
		}

		/// <summary>
		///     Load the AES key & vector from key file.
		/// </summary>
		private void LoadAesKey()
		{
			using (var writer = new BinaryReader(File.Open(Constants.AesKeyFile, FileMode.Open)))
			{
				writer.Read(_key, 0, Constants.KeySize);
				writer.Read(_vector, 0, Constants.VectorSize);
			}
		}

		/// <summary>
		///     Write all parameters to the log file(s).
		/// </summary>
		public void WriteParametersToLog()
		{
			const int maxFieldName = 36;

			// Write parameters to log file.
			_log.Info("");
			_log.Write('I', 'T', "Loaded parameters:");
			foreach (var kvp in _param)
			{
				var key = kvp.Key;
				var keyText = string.Format(
					CultureInfo.InvariantCulture,
					"{0} = ",
					key.PadRight(maxFieldName, '.'));
				_log.Write('I', ' ', keyText + kvp.Value);
			}
		}

		/// <summary>
		///     Public implementation of Dispose pattern callable by consumers.
		/// </summary>
		public void Dispose()
		{
			Dispose(true);
			GC.SuppressFinalize(this);
		}

		/// <summary>
		///     Protected implementation of Dispose pattern.
		/// </summary>
		/// <param name="disposing"></param>
		protected virtual void Dispose(bool disposing)
		{
			if (_disposed) return;

			if (disposing) DisposeMe();

			// Free any unmanaged objects here.

			_disposed = true;
		}

		/// <summary>
		///     Dispose my objects.
		/// </summary>
		private void DisposeMe()
		{
			// Dispose of the SQL connections.
			if (_param[Constants.PgConnection] != null) {
				((NpgsqlConnection) _param[Constants.PgConnection]).Dispose();
			}

			var tblDict = (Dictionary<string, DataTable>) _param[Constants.PgTables];
			foreach (var kvp in tblDict.Where(kvp => kvp.Value != null)) { kvp.Value.Dispose(); }
		}

		/// <summary>
		///     Process the ini file.
		/// </summary>
		private void ProcessIniFile()
		{
			var parser = new FileIniDataParser();
			IniData data = null;
			var iniFile = Constants.AppName + ".ini";

			try {
				data = parser.ReadFile(iniFile);
			}
			catch (ParsingException ex)
			{
				var msg = "FATAL ERROR: parsing " + iniFile + ".";
				_log.FatalException(msg, ex);
				General.Abort(msg);
			}

			if (data == null) return;

			foreach (var sect in data.Sections)
			{
				var keyComp = sect.SectionName.ToLowerInvariant();
				foreach (var pair in sect.Keys)
				{
					var kn = keyComp + "." + pair.KeyName;
					_param.Add(kn, pair.Value);
				}
			}
		}

		/// <summary>
		///     Connect to the a PostgreSQL database.
		/// </summary>
		/// <returns></returns>
		private NpgsqlConnection ConnectToPgDb(string connStr)
		{
			// Try co connect to the PostgreSQL server.
			var connString = GetPgConnectionString(connStr, false);

			NpgsqlConnection conn = null;
			try
			{
				conn = new NpgsqlConnection {ConnectionString = connString};
				conn.Open();
				return conn;
			}
			catch (Exception ex)
			{
				if (ex is NpgsqlException)
				{
					if (conn != null)
					{
						var parser = new NpgsqlConnectionStringBuilder(conn.ConnectionString);
						var host = parser.Host;
						var port = parser.Port;
						var msg = string.Format(
							CultureInfo.InvariantCulture,
							"Host: {0}:{1}\n{2}",
							host,
							port,
							ex.Message);
						_log.Write('E', Constants.LogTsType, msg);
					}
				}
				_log.WriteEx('E', Constants.LogTsType, ex);
				throw;
			}
		}

		/// <summary>
		///     Return the PG connection string based on the parsing of the .ini file.
		/// </summary>
		/// <param name="conn">The connection key in the INI file</param>
		/// <param name="test">
		///     True = use the user and password from the ini
		///     file. This is for testing purposes only
		/// </param>
		/// <returns>The connection string</returns>
		private string GetPgConnectionString(string conn, bool test)
		{
			var sb = new StringBuilder();
			var keyComp = conn + ".";

			if (_param.ContainsKey(keyComp + "server")) {
				sb.Append("Server=" + _param[keyComp + "server"] + ";");
			}

			if (_param.ContainsKey(keyComp + "port")) {
				sb.Append("Port=" + _param[keyComp + "port"] + ";");
			}

			if (_param.ContainsKey(keyComp + "database")) {
				sb.Append("database=" + _param[keyComp + "database"] + ";");
			}

			if (test) {
				sb.Append("User Id='{0}';Password='{1}';");
			}
			else
			{
				var crypto = new CryptoAes(_key, _vector);
				if (_param.ContainsKey(keyComp + "userid"))
				{
					sb.Append("User Id='" +
					          _param[keyComp + "userid"] + "';");
				}
				if (_param.ContainsKey(keyComp + "password"))
				{
					sb.Append("Password='" + crypto.DecryptString(
						_param[keyComp + "password"].ToString(), true) + "';");
				}
			}

			if (_param.ContainsKey(keyComp + "sslmode")) {
				sb.Append("SSL Mode=" + _param[keyComp + "sslmode"] + ";");
			}

			if (_param.ContainsKey(keyComp + "timeout")) {
				sb.Append("Timeout=" + _param[keyComp + "timeout"] + ";");
			}

			if (_param.ContainsKey(keyComp + "commandtimeout"))
			{
				sb.Append("CommandTimeout=" +
				          _param[keyComp + "commandtimeout"] + ";");
			}

			if (_param.ContainsKey(keyComp + "buffersize"))
			{
				sb.Append("Buffer Size=" +
				          _param[keyComp + "buffersize"] + ";");
			}

			return sb.ToString();
		}
	}
}