using System;
using System.IO;
using System.Text.RegularExpressions;

namespace CSDiscordTelegramLink
{
    public static class Logger
    {
        private static string LogFile;

        public static void Init()
        {
            if (!Directory.Exists("logs"))
            {
                Directory.CreateDirectory("logs");
            }

            LogFile = Path.Combine("logs", $"{DateTime.Now:yyyy.M.dd HH-mm-ss}.log");
            File.AppendAllText(LogFile, "");
        }

        public static void Log(object msg)
        {
            Console.ForegroundColor = ConsoleColor.White;
            Console.BackgroundColor = ConsoleColor.Black;
            Console.Write($"[{DateTime.Now}] ");
            var unformattedContent = $"[{DateTime.Now}] ";

            var parts = Regex.Split(msg.ToString(), @"(\\[cb][a-z]+\*)");
            foreach (var c in parts)
            {
                if (Regex.IsMatch(c, @"(\\c[a-z]+\*)") &&
                    c.Length > 2 &&
                    Enum.TryParse(typeof(ConsoleColor), c[2..^1], true, out var color))
                {
                    Console.ForegroundColor = (ConsoleColor)color;
                }
                else if (Regex.IsMatch(c, @"(\\b[a-z]+\*)") &&
                    c.Length > 2 &&
                    Enum.TryParse(typeof(ConsoleColor), c[2..^1], true, out var color2))
                {
                    Console.BackgroundColor = (ConsoleColor)color2;
                }
                else
                {
                    Console.Write(c);
                    unformattedContent += c;
                }
            }
            Console.Write("\n");
            unformattedContent += "\n";

            if (LogFile is not null && LogFile != "")
            {
                File.AppendAllText(LogFile, unformattedContent);
            }

            Console.ForegroundColor = ConsoleColor.White;
            Console.BackgroundColor = ConsoleColor.Black;
        }

        public static void LogDebug()
        {
            Log(@"Color tests:

\cwhite*\bblack*o\bdarkblue*o\bdarkcyan*o\bdarkgray*o\bdarkgreen*o\bdarkmagenta*o\bdarkred*o\bdarkyellow*o
\cblack*\bwhite*o\bblue*o\bcyan*o\bgray*o\bgreen*o\bmagenta*o\bred*o\byellow*o
");
            Log("Running in directory: " + Directory.GetCurrentDirectory());
            Log("OS: " + Environment.OSVersion);
            Log("Dotnet Version: " + Environment.Version);
            Log("64 bit OS: " + Environment.Is64BitOperatingSystem);
            Log("64 bit process: " + Environment.Is64BitProcess);
            Log("Machine name: " + Environment.MachineName);
            Log("Username: " + Environment.UserName);
            Log("Domain name: " + Environment.UserDomainName);
            Log("Thread ID: " + Environment.CurrentManagedThreadId);
            Log("Process ID: " + Environment.ProcessId);
            Log("Processor Count: " + Environment.ProcessorCount);
            Log("Drives: `" + string.Join("` `", Environment.GetLogicalDrives()) + "`\n");
        }
    }
}
