using System;
using System.IO;
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
            var path = @$"temp\{LastFileIndex}{extension}";

            using var stream = new FileStream(path, FileMode.OpenOrCreate);
            await telegram.DownloadFileAsync(file.FilePath, stream);
            return path;
        }

        public static void Log(object msg)
        {
            Console.WriteLine($"[{DateTime.Now}] {msg}");
        }
    }
}
