//----------------------------------------------------------------------------------------
// <copyright company="">
//     Copyright Miguel Vrolijk (c) 2015. All rights reserved.
// </copyright>
// <author>Miguel Vrolijk</author> 
// <date>2015-04-02</date>
// <time>16:00</time>
//
// <summary>IBLogger interface for Simpleinjector</summary>
//----------------------------------------------------------------------------------------

using System;
using System.Runtime.CompilerServices;

namespace ConvertPG2SS.Interfaces {
    internal interface IBLogger {
        void Write(
            char level, DateTime ts, char fmt, string text, int tab = 0,
            [CallerMemberName] string memberName = "",
            [CallerFilePath]   string filePath = "", 
            [CallerLineNumber] int lineNumber = 0);
        void Write(
            char level, char fmt, string text, int tab = 0,
            [CallerMemberName] string memberName = "",
            [CallerFilePath]   string filePath = "",
            [CallerLineNumber] int lineNumber = 0);
        void WriteEx(
            char level, char fmt, Exception ex,
            [CallerMemberName] string memberName = "",
            [CallerFilePath]   string filePath = "",
            [CallerLineNumber] int lineNumber = 0);

        bool IsTraceEnabled { get; }
        bool IsDebugEnabled { get; }
        bool IsInfoEnabled { get; }
        bool IsWarnEnabled { get; }
        bool IsErrorEnabled { get; }
        bool IsFatalEnabled { get; }

        void Trace(string format, params object[] args);
        void Debug(string format, params object[] args);
        void Info(string format, params object[] args);
        void Warn(string format, params object[] args);
        void Error(string format, params object[] args);
        void Fatal(string format, params object[] args);
        void ErrorException(string format, Exception ex);
        void FatalException(string format, Exception ex);
    }
}
