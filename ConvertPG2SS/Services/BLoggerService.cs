//----------------------------------------------------------------------------------------
// <copyright company="">
//     Copyright Miguel Vrolijk (c) 2015. All rights reserved.
// </copyright>
// <author>Miguel Vrolijk</author> 
// <date>2015-04-02</date>
// <time>16:00</time>
//
// <summary>BLogger service implementation for Simpleinjector</summary>
//----------------------------------------------------------------------------------------

using System;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;
using ConvertPG2SS.Common;
using ConvertPG2SS.Interfaces;
using NLog;
using NLog.Targets;

namespace ConvertPG2SS.Services {
	/// <summary>
	///     Wraps a Logger instance.
	/// </summary>
	class BLoggerService : Logger, IBLogger {
		private const string Bullets = "*-········";
#if DEBUG
		private static readonly string FileName = "${basedir}/logs/" + Constants.AppName + ".log";
#else
		private static readonly string FileName =
			"${basedir}/logs/" + Constants.AppName + "_" +
					DateTime.Now.ToString(Constants.LogNameDateTime).Trim() + ".log";
#endif

		/// <summary>
		///     The logger field.
		/// </summary>
		private readonly Logger _logger;

		public BLoggerService() {
			try {
#if DEBUG
				foreach (var rule in LogManager.Configuration.LoggingRules) {
					rule.EnableLoggingForLevel(LogLevel.Trace);
				}
#endif
				//_logger = LogManager.GetCurrentClassLogger();
				var target = (FileTarget)LogManager.Configuration.FindTargetByName("logfile");
				//target.FileName = "${basedir}/logs/" + Constants.AppName + ".log";
				target.FileName = FileName;
#if DEBUG
				target.DeleteOldFileOnStartup = true;
#endif
				LogManager.ReconfigExistingLoggers();
				_logger = LogManager.GetCurrentClassLogger();
			}
			catch (NullReferenceException nrex) {
				Console.WriteLine(nrex.ToString());
				Environment.Exit(1);
			}
		}

		/* public FpLogger(string name) {
			_logger = LogManager.GetLogger(name);
		} */

		/// <summary>
		///     Write text to the log file(s). This method has the addition of the
		///     timestamp parameter. It's used primarily to print log records from
		///     the database.
		/// </summary>
		/// <param name="level">See the Format method for details.</param>
		/// <param name="ts">Idem.</param>
		/// <param name="fmt">Idem.</param>
		/// <param name="text">Idem.</param>
		/// <param name="tab">Idem.</param>
		/// <param name="memberName"></param>
		/// <param name="filePath"></param>
		/// <param name="lineNumber"></param>
		public void Write(
			char level,
			DateTime ts,
			char fmt,
			string text,
			int tab = 0,
			[CallerMemberName] string memberName = "",
			[CallerFilePath] string filePath = "",
			[CallerLineNumber] int lineNumber = 0) 
		{
			_write(level, ts, fmt, text, tab, memberName, filePath, lineNumber);
		}

		/// <summary>
		///     Write text to the log file(s). This is the main method used by the
		///     application to write to the log file(s).
		/// </summary>
		/// <param name="level">See the Format method for details.</param>
		/// <param name="fmt">Idem.</param>
		/// <param name="text">Idem.</param>
		/// <param name="tab">Idem.</param>
		/// <param name="memberName"></param>
		/// <param name="filePath"></param>
		/// <param name="lineNumber"></param>
		public void Write(
			char level,
			char fmt,
			string text,
			int tab = 0,
			[CallerMemberName] string memberName = "",
			[CallerFilePath] string filePath = "",
			[CallerLineNumber] int lineNumber = 0) 
		{
			_write(level, DateTime.Now, fmt, text, tab, memberName, filePath, lineNumber);
		}

		/// <summary>
		///     Write an exception including the stack trace.
		/// </summary>
		/// <param name="level">See the Format method for details.</param>
		/// <param name="fmt">Idem.</param>
		/// <param name="ex">The exception.</param>
		/// <param name="memberName"></param>
		/// <param name="filePath"></param>
		/// <param name="lineNumber"></param>
		public void WriteEx(
			char level,
			char fmt,
			Exception ex,
			[CallerMemberName] string memberName = "",
			[CallerFilePath] string filePath = "",
			[CallerLineNumber] int lineNumber = 0) 
		{
			_write(
				level,
				DateTime.Now,
				fmt,
				ex + "\n\n" + ex.StackTrace,
				0,
				memberName,
				filePath,
				lineNumber);
		}

		/// <summary>
		///     Overload the NLog Is...Enabled methods.
		/// </summary>

		#region Is...Enabled methods
		public new bool IsTraceEnabled {
			get { return _logger.IsTraceEnabled; }
		}

		public new bool IsDebugEnabled {
			get { return _logger.IsDebugEnabled; }
		}

		public new bool IsInfoEnabled {
			get { return _logger.IsInfoEnabled; }
		}

		public new bool IsWarnEnabled {
			get { return _logger.IsWarnEnabled; }
		}

		public new bool IsErrorEnabled {
			get { return _logger.IsErrorEnabled; }
		}

		public new bool IsFatalEnabled {
			get { return _logger.IsFatalEnabled; }
		}

		#endregion

		/// <summary>
		///     Overload the NLog write methods (Trace, Debug, Warn, etc).
		/// </summary>
		/// <param name="format">The text to be logged.</param>
		/// <param name="args">Optional array of objects.</param>

		#region WriteLog methods
		public new void Trace(string format, params object[] args) {
			if (_logger.IsTraceEnabled) WriteLog(LogLevel.Trace, format, args);
		}

		public new void Debug(string format, params object[] args) {
			if (_logger.IsDebugEnabled) WriteLog(LogLevel.Debug, format, args);
		}

		public new void Info(string format, params object[] args) {
			if (_logger.IsInfoEnabled) WriteLog(LogLevel.Info, format, args);
		}

		public new void Warn(string format, params object[] args) {
			if (_logger.IsWarnEnabled) WriteLog(LogLevel.Warn, format, args);
		}

		public new void Error(string format, params object[] args) {
			if (_logger.IsErrorEnabled) WriteLog(LogLevel.Error, format, args);
		}

		public new void Fatal(string format, params object[] args) {
			if (_logger.IsFatalEnabled) WriteLog(LogLevel.Fatal, format, args);
		}

		public new void ErrorException(string format, Exception ex) {
			if (_logger.IsErrorEnabled) WriteLogExc(LogLevel.Error, format, ex, null);
		}

		public new void FatalException(string format, Exception ex) {
			if (_logger.IsFatalEnabled) WriteLogExc(LogLevel.Fatal, format, ex, null);
		}

		/// <summary>
		///     Format a string according to the parameters.
		/// </summary>
		/// <param name="level">Logging level, e.g. 'I' = Info, 'W' = Warn.</param>
		/// <param name="ts">Timestamp.</param>
		/// <param name="fmt">
		///     Timestamp format:
		///     ' ' = No timestamp info.
		///     'T' = The whole time stamp.
		///     'D' = Only the date portion.
		///     Constants.LogTsType = Only the time portion.
		/// </param>
		/// <param name="text">The text to be logged.</param>
		/// <param name="tab">The tab level.</param>
		/// <param name="method"></param>
		/// <param name="filePath"></param>
		/// <param name="lineNumber"></param>
		/// <returns>The formatted text.</returns>
		private static string Format(
			char level,
			DateTime ts,
			char fmt,
			string text,
			int tab,
			string method = "",
			string filePath = "",
			int lineNumber = 0) {
			var tabInd = new StringBuilder(32);
			var dspMsg = new StringBuilder(256);
			if (tab > 0 && tab <= 10) {
				if (tab > 1) tabInd.Append(' ', (tab - 1) * 3);
				tabInd.Append(" " + Bullets.Substring(tab - 1, 1) + " ");
			}

			var fts = "";
			switch (fmt) {
				case 'Z':
					fts = ts.ToString("HH':'mm':'ss.ffff ", CultureInfo.InvariantCulture);
					break;
				case 'T':
					fts = ts.ToString(
						"yyyy'-'MM'-'dd' 'HH':'mm':'ss.ffff ", CultureInfo.InvariantCulture);
					break;
				case 'H':
					fts = ts.ToString("HH':'mm':'ss ", CultureInfo.InvariantCulture);
					break;
				case 'D':
					fts = ts.ToString("yyyy'-'MM'-'dd ", CultureInfo.InvariantCulture);
					break;
			}

			if (!string.IsNullOrEmpty(method)) {
				dspMsg.Append(fts + "[" + level + "] ");
				dspMsg.Append(filePath + " - " + method + ": " + lineNumber + "\n");
			}
			dspMsg.Append(fts + "[" + level + "] " + tabInd + text);
			return dspMsg.ToString();
		}

		#endregion

		/// <summary>
		///     Write text to the log file(s). This method has the addition of the
		///     timestamp parameter. It's used primarily to print log records from
		///     the database.
		/// </summary>
		/// <param name="level">See the Format method for details.</param>
		/// <param name="ts">Idem.</param>
		/// <param name="fmt">Idem.</param>
		/// <param name="text">Idem.</param>
		/// <param name="tab">Idem.</param>
		/// <param name="memberName"></param>
		/// <param name="filePath"></param>
		/// <param name="lineNumber"></param>
		private void _write(
			char level,
			DateTime ts,
			char fmt,
			string text,
			int tab = 0,
			string memberName = "",
			string filePath = "",
			int lineNumber = 0) 
		{
			string dspMsg;
			if ((level.Equals('E') || level.Equals('F')) &&
				!string.IsNullOrEmpty(memberName)) {
				dspMsg = Format(level, ts, fmt, text, tab, memberName, filePath, lineNumber);
			}
			else dspMsg = Format(level, ts, fmt, text, tab);

			switch (level) {
				case 'T': _logger.Trace(dspMsg); break;
				case 'D': _logger.Debug(dspMsg); break;
				case 'I': _logger.Info(dspMsg); break;
				case 'W': _logger.Warn(dspMsg); break;
				case 'E': _logger.Error(dspMsg); break;
				case 'F': _logger.Fatal(dspMsg); break;
			}
		}

		private void WriteLog(LogLevel level, string format, params object[] args) {
			var le = new LogEventInfo(level, _logger.Name, null, format, args);
			_logger.Log(typeof(BLoggerService), le);
		}

		private void WriteLogExc(LogLevel level, string format,
			Exception ex, params object[] args) {
			var le = new LogEventInfo(level, _logger.Name, null, format, args, ex);
			_logger.Log(typeof(BLoggerService), le);
		}
	}
}