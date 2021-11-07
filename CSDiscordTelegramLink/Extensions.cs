using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

using Discord.WebSocket;

using Telegram.Bot;

namespace CSDiscordTelegramLink
{
    public static class Extensions
    {
        private static int LastFileIndex;
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

        public static string CleanDiscordMessage(this DiscordSocketClient client, SocketMessage msg)
        {
            var cleanContent = msg.Content;

            var user = msg.Author as SocketGuildUser;
            var username = user.Nickname ?? user.Username;

            username = Regex.Replace(username, @"([.*\-+!#(){}|><_=])", m =>
            {
                if (m is null || m.Groups.Count < 2) { return ""; }
                return $"\\{m.Groups[1]}";
            });

            if (msg.Content is null)
            {
                return $"*__{username}__*";
            }

            cleanContent = Regex.Replace(cleanContent, @"<:([^:]+):\d+>", m =>
            {
                if (m is null || m.Groups.Count < 2) { return ":?"; }
                return $":{m.Groups[1]}:";
            });

            cleanContent = Regex.Replace(cleanContent, @"<@!?(\d+)>", m =>
            {
                if (m is null || m.Groups.Count < 2) { return "@?1"; }
                if (!ulong.TryParse(m.Groups[1].ToString(), out var id)) { return "@?2"; }
                var user = client.GetUser(id);
                if (user is null || user.Username is null) { return "@?3"; }
                return "@ " + user.Username;
            });

            cleanContent = Regex.Replace(cleanContent, @"<#!?(\d+)>", m =>
            {
                if (m is null || m.Groups.Count < 2) { return "#?1"; }
                if (!ulong.TryParse(m.Groups[1].ToString(), out var id)) { return "#?2"; }
                var channel = client.GetChannel(id);
                if (channel is null) { return "#?"; }
                if (channel is not SocketTextChannel textChannel || textChannel.Name is null) { return "#?3"; }
                return textChannel.Name;
            });

            cleanContent = Regex.Replace(cleanContent, @"([.*\-+!#(){}|><_=])", m =>
            {
                if (m is null || m.Groups.Count < 2) { return ""; }
                return $"\\{m.Groups[1]}";
            });

            cleanContent = $"*__{username}__* \n{cleanContent}";
            return cleanContent;
        }

        public static List<string> FormatSplit(this string s, int chunkSize, char separator)
        {
            var toReturn = new List<string>();
            if (s is null || s.Trim().Length <= 0) { return toReturn; }

            s = s.Trim();

            while (s.Length > 0)
            {
                if (s.Length < chunkSize)
                {
                    toReturn.Add(s);
                    break;
                }

                var i = s.Substring(0, chunkSize).LastIndexOf("\n");
                if (i < 0)
                {
                    var p = s.Substring(0, chunkSize);
                    s = s[chunkSize..];
                    toReturn.Add(p);
                }
                else
                {
                    var p = s.Substring(0, i);
                    s = s[(i + 1)..];
                }
            }

            return toReturn;
        }
    }
}
