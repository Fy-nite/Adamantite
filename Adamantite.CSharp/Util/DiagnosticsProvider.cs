using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;

namespace Adamantite.Util
{
    public static class DiagnosticsProvider
    {
        public enum Level { Info, Warning, Error, Debug }

        private struct Ansi
        {
            public const string Reset = "\x1b[0m";
            public const string Bold = "\x1b[1m";
            public const string Dim = "\x1b[2m";
            public const string Red = "\x1b[31m";
            public const string Green = "\x1b[32m";
            public const string Yellow = "\x1b[33m";
            public const string Blue = "\x1b[34m";
            public const string Magenta = "\x1b[35m";
            public const string Cyan = "\x1b[36m";
            public const string Gray = "\x1b[90m";
        }

        private static bool EnableVirtualTerminalIfPossible()
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) return true;

            try
            {
                const int STD_ERROR_HANDLE = -12;
                var handle = GetStdHandle(STD_ERROR_HANDLE);
                if (handle == IntPtr.Zero || handle == INVALID_HANDLE_VALUE) return false;

                if (!GetConsoleMode(handle, out uint mode)) return false;
                mode |= ENABLE_VIRTUAL_TERMINAL_PROCESSING;
                return SetConsoleMode(handle, mode);
            }
            catch
            {
                return false;
            }
        }

        public static void Emit(Level level, string header = "", string contents = "", IEnumerable<object>? lastStack = null, IEnumerable<string>? callStack = null)
        {
            bool useColor = EnableVirtualTerminalIfPossible();
            var a = new Ansi();

            string LevelName() => level switch
            {
                Level.Info => "INFO",
                Level.Warning => "WARN",
                Level.Error => "ERROR",
                Level.Debug => "DEBUG",
                _ => "LOG",
            };

            string LevelColor() => level switch
            {
                Level.Info => Ansi.Cyan,
                Level.Warning => Ansi.Yellow,
                Level.Error => Ansi.Red,
                Level.Debug => Ansi.Magenta,
                _ => Ansi.Gray,
            };

            // Title
            if (useColor) Console.Error.Write(Ansi.Bold + LevelColor());
            Console.Error.Write($"\n== {LevelName()} ==");
            if (useColor) Console.Error.Write(Ansi.Reset);
            if (!string.IsNullOrEmpty(header))
            {
                Console.Error.Write(" ");
                if (useColor) Console.Error.Write(Ansi.Bold);
                Console.Error.Write(header);
                if (useColor) Console.Error.Write(Ansi.Reset);
            }
            Console.Error.WriteLine();

            if (!string.IsNullOrEmpty(contents))
            {
                if (useColor) Console.Error.Write(Ansi.Gray);
                Console.Error.WriteLine(contents);
                if (useColor) Console.Error.Write(Ansi.Reset);
            }

            // Call stack
            if (callStack != null)
            {
                var frames = callStack.ToList();
                if (frames.Count > 0)
                {
                    if (useColor) Console.Error.Write(Ansi.Bold + Ansi.Cyan);
                    Console.Error.WriteLine("\nCall Stack (most recent call first):");
                    if (useColor) Console.Error.Write(Ansi.Reset);
                    for (int i = 0; i < frames.Count; ++i)
                    {
                        Console.Error.WriteLine($"  #{i} {frames[i]}");
                    }
                }
            }

            // Eval stack (top first)
            var snapshot = lastStack?.ToList() ?? new List<object>();
            if (snapshot.Count > 0)
            {
                if (useColor) Console.Error.Write(Ansi.Bold + Ansi.Cyan);
                Console.Error.WriteLine("\nEval Stack (top first):");
                if (useColor) Console.Error.Write(Ansi.Reset);
                for (int i = 0; i < snapshot.Count; ++i)
                {
                    Console.Error.WriteLine($"  [{i}] {ValueToPrettyString(snapshot[i], useColor)}");
                }
            }
        }

        public static void Info(string header, IEnumerable<object>? lastStack = null, string contents = "") => Emit(Level.Info, header, contents, lastStack);
        public static void Warning(string header, IEnumerable<object>? lastStack = null, string contents = "") => Emit(Level.Warning, header, contents, lastStack);
        public static void Error(string header, IEnumerable<object>? lastStack = null, string contents = "") => Emit(Level.Error, header, contents, lastStack);
        public static void Debug(string header, IEnumerable<object>? lastStack = null, string contents = "") => Emit(Level.Debug, header, contents, lastStack);

        private static string ValueToPrettyString(object? v, bool useColor)
        {
            if (v == null) return useColor ? Ansi.Gray + "null" + Ansi.Reset : "null";

            switch (v)
            {
                case bool b: return (useColor ? Ansi.Yellow : "") + (b ? "true" : "false") + (useColor ? Ansi.Reset : "");
                case int i: return (useColor ? Ansi.Green : "") + "int32 " + i + (useColor ? Ansi.Reset : "");
                case long l: return (useColor ? Ansi.Green : "") + "int64 " + l + (useColor ? Ansi.Reset : "");
                case float f: return (useColor ? Ansi.Green : "") + "float32 " + f + (useColor ? Ansi.Reset : "");
                case double d: return (useColor ? Ansi.Green : "") + "float64 " + d + (useColor ? Ansi.Reset : "");
                case string s:
                {
                    var outStr = s.Length > 160 ? s.Substring(0, 157) + "..." : s;
                    return (useColor ? Ansi.Blue : "") + "string \"" + (useColor ? Ansi.Reset : "") + outStr + (useColor ? Ansi.Blue : "") + "\"" + (useColor ? Ansi.Reset : "");
                }
                default:
                {
                    var typeName = v.GetType().Name;
                    return (useColor ? Ansi.Magenta : "") + typeName + (useColor ? Ansi.Reset : "") + " @" + v.GetHashCode();
                }
            }
        }

        // Windows console interop
        private const uint ENABLE_VIRTUAL_TERMINAL_PROCESSING = 0x0004;
        private static readonly IntPtr INVALID_HANDLE_VALUE = new IntPtr(-1);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr GetStdHandle(int nStdHandle);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool GetConsoleMode(IntPtr hConsoleHandle, out uint lpMode);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool SetConsoleMode(IntPtr hConsoleHandle, uint dwMode);
    }
}
