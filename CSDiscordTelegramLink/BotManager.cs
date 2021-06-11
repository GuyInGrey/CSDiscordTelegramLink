using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

using Discord;
using Discord.Webhook;
using Discord.WebSocket;

using Newtonsoft.Json.Linq;

using Telegram.Bot;
using Telegram.Bot.Args;
using Telegram.Bot.Types;
using Telegram.Bot.Types.InputFiles;

namespace CSDiscordTelegramLink
{
    public class BotManager
    {
        public JObject Config;

        public DiscordSocketClient DiscordClient;
        public DiscordWebhookClient DiscordWebhook;
        public DiscordWebhookClient DiscordWebhook2;
        public bool WhichWebhook = true;
        public string LastWebhookName = "";
        public ulong GuildID;
        public ulong ChannelID;

        public TelegramBotClient TelegramClient;
        public ChatId TelegramGroup;
        public long LastFileIndex = 0;

        // (Telegram, Discord)
        public List<(int, ulong)> MessageHistory = new List<(int, ulong)>();

        public BotManager()
        {
            // Load config
            Config = JObject.Parse(System.IO.File.ReadAllText(@"C:\ISLE\config.json"));

            // Reset temp files
            if (Directory.Exists("temp"))
            {
                Directory.Delete("temp", true);
            }
            Directory.CreateDirectory("temp");

            // Startup
            Console.WriteLine("Starting up...");
            SetupTelegramBot();
            SetupDiscordBot().GetAwaiter().GetResult();

            TelegramGroup = new ChatId(long.Parse(Config["telegramGroupID"].Value<string>()));
            GuildID = ulong.Parse(Config["discordGuildId"].Value<string>());
            ChannelID = ulong.Parse(Config["discordGeneralChannel"].Value<string>());
            Console.WriteLine("Ready.");
        }

        public async Task SetupDiscordBot()
        {
            var discordConfig = new DiscordSocketConfig()
            {
                DefaultRetryMode = RetryMode.AlwaysRetry,
            };
            DiscordClient = new DiscordSocketClient(discordConfig);
            var token = Config["discordToken"].Value<string>();

            DiscordClient.MessageReceived += DiscordClient_MessageReceived;

            await DiscordClient.LoginAsync(TokenType.Bot, token);
            await DiscordClient.StartAsync();
            Console.WriteLine("Discord bot started.");

            var webhookUrl = Config["discordWebhookUrl"].Value<string>();
            DiscordWebhook = new DiscordWebhookClient(webhookUrl);
            var webhookUrl2 = Config["discordWebhookUrl2"].Value<string>();
            DiscordWebhook2 = new DiscordWebhookClient(webhookUrl2);
            Console.WriteLine("Discord webhook started.");
        }

        private async Task DiscordClient_MessageReceived(SocketMessage arg)
        {
            if (arg.Author.IsBot || arg.Source == MessageSource.System) { return; }
            if (arg.Channel.Id != ChannelID) { return; }

            var user = arg.Author.Username;
            var cleanContent = Regex.Replace(arg.Content, @"<:([^:]+):\d+>", m =>
            {
                try
                {
                    var emoteName = m.Groups[1].ToString();
                    return $":{emoteName}:";
                }
                catch { return ":?:"; }
            });

            cleanContent = Regex.Replace(cleanContent, @"<@!?(\d+)>", m =>
            {
                try
                {
                    var id = ulong.Parse(m.Groups[1].ToString());
                    var user = DiscordClient.GetUser(id);
                    if (user is null || user.Username is null) { return "@?"; }
                    return "@" + user.Username;
                }
                catch { return "@?"; }
            });
            
            cleanContent = Regex.Replace(cleanContent, @"<#!?(\d+)>", m =>
            {
                try
                {
                    var id = ulong.Parse(m.Groups[1].ToString());
                    var channel = DiscordClient.GetChannel(id) as SocketTextChannel;
                    return "#" + channel.Name;
                }
                catch { return "#?"; }
            });

            cleanContent =
                cleanContent.Replace(".", "\\.")
                .Replace("*", "\\*")
                .Replace("-", "\\-")
                .Replace("+", "\\-")
                .Replace("!", "\\!")
                .Replace("#", "\\#")
                .Replace(")", "\\)")
                .Replace("(", "\\(")
                .Replace("}", "\\}")
                .Replace("{", "\\{")
                .Replace("|", "\\|")
                .Replace(">", "\\>")
                .Replace("<", "\\<")
                .Replace("_", "\\_");

            var content = $"*__{user}__* \n{cleanContent}";

            if (content is not null && content != "")
            {
                var msg = await TelegramClient.SendTextMessageAsync(TelegramGroup, content, Telegram.Bot.Types.Enums.ParseMode.MarkdownV2);
                MessageHistory.Add((msg.MessageId, arg.Id));
            }

            foreach (var a in arg.Attachments)
            {
                try
                {
                    await TelegramClient.SendDocumentAsync(TelegramGroup, new InputOnlineFile(a.Url));
                }
                catch
                {
                    await arg.AddReactionAsync(new Emoji("⚠"));
                }
            }
        }

        public void SetupTelegramBot()
        {
            var token = Config["telegramToken"].Value<string>();
            TelegramClient = new TelegramBotClient(token);

            TelegramClient.OnMessage += OnTelegramMessage;
            TelegramClient.StartReceiving();

            Console.WriteLine("Telegram bot started.");
        }

        private async void OnTelegramMessage(object sender, MessageEventArgs e)
        {
            //if ((new ChatId(e.Message.Chat.Id)) != TelegramGroup) { return; }

            if (DiscordClient.LoginState != LoginState.LoggedIn || 
                DiscordClient.ConnectionState != ConnectionState.Connected ||
                DiscordWebhook is null ||
                DiscordWebhook2 is null ||
                e.Message.From.IsBot)
            {
                return;
            }

            var name = e.Message.From.FirstName + " " + e.Message.From.LastName;

            if (LastWebhookName != name) { WhichWebhook ^= true; }
            LastWebhookName = name;
            var hook = WhichWebhook ? DiscordWebhook : DiscordWebhook2;

            var avatars = (await TelegramClient.GetUserProfilePhotosAsync(e.Message.From.Id))?.Photos;
            if (avatars.Length == 0 || avatars[0].Length == 0)
            {
                await hook.ModifyWebhookAsync(w =>
                {
                    w.Image = new Image("unknown.png");
                });
            }
            else
            {
                var avatarPath = await DownloadTelegramFile(avatars[0][0].FileId);
                await hook.ModifyWebhookAsync(w =>
                {
                    w.Image = new Image(avatarPath);
                });
            }

            var replyId = e.Message.ReplyToMessage?.MessageId;

            var text = (e.Message.Text is object && e.Message.Text != "") ? e.Message.Text :
                (e.Message.Caption is object && e.Message.Caption != "") ? e.Message.Caption : "";

            if (replyId != default) 
            {
                var linked = MessageHistory.FirstOrDefault(m => m.Item1 == replyId);
                if (linked != default)
                {
                    var url = $"https://discord.com/channels/{GuildID}/{ChannelID}/{linked.Item2}";
                    text = $"[Reply <:cursor:852946682509262899>]({url})\n{text}";
                }
            }

            ulong id = 0;

            if (e.Message.Type == Telegram.Bot.Types.Enums.MessageType.Text)
            {
                id = await hook.SendMessageAsync(
                    text,
                    username: name);
            }
            else if (e.Message.Type == Telegram.Bot.Types.Enums.MessageType.Photo)
            {
                var file = await DownloadTelegramFile(e.Message.Photo.Last().FileId);
                id = await hook.SendFileAsync(
                    filePath: file,
                    text: text,
                    username: name);
            }
            else if (e.Message.Type == Telegram.Bot.Types.Enums.MessageType.Document)
            {
                var file = await DownloadTelegramFile(e.Message.Document.FileId);
                id = await hook.SendFileAsync(
                    filePath: file,
                    text: text,
                    username: name);
            }
            else if (e.Message.Type == Telegram.Bot.Types.Enums.MessageType.Sticker)
            {
                id = await hook.SendMessageAsync(
                    text: $"{e.Message.Sticker.Emoji}\n{text}",
                    username: name);
            }
            else if (e.Message.Type == Telegram.Bot.Types.Enums.MessageType.Audio)
            {
                var file = await DownloadTelegramFile(e.Message.Audio.FileId);
                id = await hook.SendFileAsync(
                    filePath: file,
                    text: text,
                    username: name);
            }

            if (id != 0)
            {
                MessageHistory.Add((e.Message.MessageId, id));
            }
        }

        public async Task<string> DownloadTelegramFile(string fileId)
        {
            var file = await TelegramClient.GetFileAsync(fileId);
            var extension = Path.GetExtension(file.FilePath);

            LastFileIndex++;
            var path = @$"temp\{LastFileIndex}{extension}";

            using var stream = new FileStream(path, FileMode.OpenOrCreate);
            await TelegramClient.DownloadFileAsync(file.FilePath, stream);
            return path;
        }
    }
}
