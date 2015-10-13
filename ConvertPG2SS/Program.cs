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
// <summary>Convert a PostgreSQL 9.4 database to Sql Server 2012.</summary>
//----------------------------------------------------------------------------------------

using System;
using System.Diagnostics;
using ConvertPG2SS.Common;
using ConvertPG2SS.Helpers;
using ConvertPG2SS.Interfaces;
using ConvertPG2SS.Services;
using NLog;
using SimpleInjector;

namespace ConvertPG2SS
{
	internal class Program
	{
		private static Container _container;
		private static IBLogger _log;
		private static IParameters _param;

		/// <summary>
		///     Simple Injection GetInstance method.
		/// </summary>
		/// <typeparam name="TService"></typeparam>
		/// <returns></returns>
		[DebuggerStepThrough]
		internal static TService GetInstance<TService>() where TService : class
		{
			return _container.GetInstance<TService>();
		}

		private static void Main()
		{
			Bootstrap();
			_log = GetInstance<IBLogger>();

			_log.Info(new string('-', 160));
			_log.Write('I', Constants.LogTsType, "Program " + Constants.AppName + " started.");
			_log.Info(new string('-', 160));

			_param = GetInstance<IParameters>();
#if DEBUG
			_param.WriteParametersToLog();
#endif
			if (General.CheckParams(_param, _log))
			{
				if (bool.Parse(_param[Parameters.OtherPg2Ss].ToString()))
					if (ProcessPgSchema.Do()) { ProcessPgBulk.Do(); }
			}

			_param.Dispose();
			_log.Info(string.Empty);
			_log.Write('I', Constants.LogTsType, "Program " + Constants.AppName + " ended.");
#if DEBUG
			Console.ReadLine();
#endif
			if (LogManager.GetCurrentClassLogger() != null) LogManager.Flush();
		}

		/// <summary>
		///     Create the Simple Injector Container objects.
		/// </summary>
		private static void Bootstrap()
		{
			var container = new Container();

			// Register types.
			container.RegisterSingleton<IBLogger, BLoggerService>();
			container.RegisterSingleton<IParameters, ParametersService>();

			container.Verify();
			_container = container;
		}
	}
}