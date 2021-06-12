using System;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

using Telegram.Bot;

namespace CSDiscordTelegramLink
{
    public static class Extensions
    {
        public static int LastFileIndex;
        public static async Task<string> DownloadTelegramFile(this TelegramBotClient telegram, string fileId)
        {
            var file = await telegram.GetFileAsync(fileId);
            var extension = Path.GetExtension(file.FilePath);

            LastFileIndex++;
            var path = Path.Combine("temp", $"{LastFileIndex}{extension}");

            using var stream = new FileStream(path, FileMode.OpenOrCreate);
            await telegram.DownloadFileAsync(file.FilePath, stream);
            return path;
        }

        public static void Log(object msg)
        {
            Console.ForegroundColor = ConsoleColor.White;
            Console.BackgroundColor = ConsoleColor.Black;
            Console.Write($"[{DateTime.Now}] ");

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
                }

            }
            Console.Write("\n");
        }
    }
}
