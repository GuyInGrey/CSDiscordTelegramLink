using System;
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
        public DiscordSocketClient DiscordClient;
        public DiscordWebhookClient DiscordWebhook;
        public DiscordWebhookClient DiscordWebhook2;
        public bool WhichWebhook = true;
        public string LastWebhookName = "";
        public ChatId TelegramGroup;

        public long LastFileIndex = 0;

        public TelegramBotClient TelegramClient;

        public JObject Config;

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
            if (arg.Author.IsBot) { return; }
            if (arg.Channel.Id != ulong.Parse(Config["discordGeneralChannel"].Value<string>())) { return; }

            var user = arg.Author.Username;
            var cleanContent = Regex.Replace(arg.Content, @"<@!?(\d+)>", m =>
            {
                try
                {
                    var id = ulong.Parse(m.Groups[1].ToString());
                    var user = DiscordClient.GetUser(id);
                    return "@" + user.Username;
                }
                catch
                {
                    return "@?";
                }
            });

            cleanContent = Regex.Replace(cleanContent, @"<#!?(\d+)>", m =>
            {
                try
                {
                    var id = ulong.Parse(m.Groups[1].ToString());
                    var channel = DiscordClient.GetChannel(id) as SocketTextChannel;
                    return "#" + channel.Name;
                }
                catch
                {
                    return "#?";
                }
            });

            var content = $"*__{user}__* \n{cleanContent}";

            if (content is not null && content != "")
            {
                await TelegramClient.SendTextMessageAsync(TelegramGroup, content, Telegram.Bot.Types.Enums.ParseMode.MarkdownV2);
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
            Console.WriteLine(e.Message.Chat.Id);
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

            var avatars = await TelegramClient.GetUserProfilePhotosAsync(e.Message.From.Id);
            var avatarPath = await DownloadTelegramFile(avatars.Photos[0][0].FileId);
            await hook.ModifyWebhookAsync(w =>
            {
                w.Image = new Image(avatarPath);
            });

            if (e.Message.Type == Telegram.Bot.Types.Enums.MessageType.Text)
            {
                await hook.SendMessageAsync(
                    e.Message.Text,
                    username: name);
            }
            else if (e.Message.Type == Telegram.Bot.Types.Enums.MessageType.Photo)
            {
                var file = await DownloadTelegramFile(e.Message.Photo.Last().FileId);
                await hook.SendFileAsync(
                    filePath: file,
                    text: e.Message.Caption,
                    username: name);
            }
            else if (e.Message.Type == Telegram.Bot.Types.Enums.MessageType.Document)
            {
                var file = await DownloadTelegramFile(e.Message.Document.FileId);
                await hook.SendFileAsync(
                    filePath: file,
                    text: e.Message.Caption,
                    username: name);
            }
        }

        public async Task<string> DownloadTelegramFile(string fileId)
        {
            LastFileIndex++;
            var file = await TelegramClient.GetFileAsync(fileId);
            var extension = Path.GetExtension(file.FilePath);

            var path = @$"temp\{LastFileIndex}{extension}";

            using var stream = new FileStream(path, FileMode.OpenOrCreate);
            await TelegramClient.DownloadFileAsync(file.FilePath, stream);
            return path;
        }
    }
}
