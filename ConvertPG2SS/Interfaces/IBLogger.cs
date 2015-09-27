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
