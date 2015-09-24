//----------------------------------------------------------------------------------------
// <copyright company="">
//     Copyright Miguel Vrolijk (c). All rights reserved.
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
using ConvertPG2SS.Interfaces;
using ConvertPG2SS.Services;
using NLog;
using SimpleInjector;

namespace ConvertPG2SS {
	class Program {
		private static Container _container;
		private static IBLogger _log;
		private static IParameters _param;

		/// <summary>
		///     Simple Injection GetInstance method.
		/// </summary>
		/// <typeparam name="TService"></typeparam>
		/// <returns></returns>
		[DebuggerStepThrough]
		internal static TService GetInstance<TService>() where TService : class {
			return _container.GetInstance<TService>();
		}

		static void Main() {
			Bootstrap();
			_log = GetInstance<IBLogger>();

			_log.Info(new string('-', 160));
			_log.Write('I', Constants.LogTsType, "Program " + Constants.AppName + " started.");
			_log.Info(new string('-', 160));

			_param = GetInstance<IParameters>();
#if DEBUG
			_param.WriteParametersToLog();
#endif
			// var success = ProcessImportFiles.Go();

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
		private static void Bootstrap() {
			var container = new Container();

			// Register types.
			container.RegisterSingleton<IBLogger, BLoggerService>();
			container.RegisterSingleton<IParameters, ParametersService>();

			container.Verify();
			_container = container;
		}
	}
}
