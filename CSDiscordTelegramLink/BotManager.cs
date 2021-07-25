using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

using Discord;
using Discord.WebSocket;

using Newtonsoft.Json.Linq;

using Telegram.Bot;

namespace CSDiscordTelegramLink
{
    public class BotManager
    {
        public static JObject Config;

        public DiscordSocketClient DiscordClient;
        public TelegramBotClient TelegramClient;
        public List<Link> ActiveLinks = new();
        public TicketManager TicketManager = new(2);

        public BotManager()
        {
            Logger.Log("Starting up...");

            // Load config
            Logger.Log("Loading configuration...");
            var configText = File.ReadAllText("config.json").Replace("\r", "");
            Config = JObject.Parse(configText);
            Logger.Log("Configuration loaded.");

            // Reset temp files
            Logger.Log("Resetting temp directory...");
            if (Directory.Exists("temp"))
            {
                Directory.Delete("temp", true);
            }
            Directory.CreateDirectory("temp");
            Logger.Log("Temp directory reset.");

            // Database connection
            var dbLoaded = ReplyManager.Init(DatabaseCredentials.FromJson(Config["database"] as JObject));
            if (!dbLoaded)
            {
                Logger.Log("Failed to connect to database, aborting.");
                Environment.Exit(1);
            }

            // Startup
            SetupTelegramBot();
            SetupDiscordBot();

            Logger.Log("Creating links...");

            var avatarChannelId = ulong.Parse(Config["discordAvatarChannel"].Value<string>());
            var links = Config["links"].Value<JArray>();
            foreach (JObject linkToken in links)
            {
                var link = Link.FromJson(linkToken, avatarChannelId);
                ActiveLinks.Add(link);
            }

            Logger.Log("Activating links...");
            ActiveLinks.ForEach(l => l.Listen(DiscordClient, TelegramClient, TicketManager));

            BeginListening().GetAwaiter().GetResult();
        }

        private async Task BeginListening()
        {
            var token = Config["discordToken"].Value<string>();
            await DiscordClient.LoginAsync(TokenType.Bot, token);

            await DiscordClient.StartAsync();

            var status = Config["discordStatus"].Value<string>();
            if (status != "")
            {
                await DiscordClient.SetGameAsync(status);
            }
            Logger.Log("Discord bot started.");

            TelegramClient.StartReceiving();
            Logger.Log("Telegram bot started.");
        }

        public void SetupDiscordBot()
        {
            var discordConfig = new DiscordSocketConfig()
            {
                DefaultRetryMode = RetryMode.AlwaysRetry,
                AlwaysDownloadUsers = true,
                ExclusiveBulkDelete = true,
            };
            DiscordClient = new DiscordSocketClient(discordConfig);

            DiscordClient.Log += (msg) =>
            {
                var prefix = @"\cblue*[Discord]\cwhite* ";
                Logger.Log(prefix + msg.Message);
                if (msg.Exception is not null)
                {
                    Logger.Log($"{prefix}\\cred*{msg.Exception.Message}");
                    Logger.Log($"{prefix}\\cred*{msg.Exception.StackTrace.Replace("\n", "\n\\cred*")}");
                }

                return Task.CompletedTask;
            };

            DiscordClient.Ready += async () =>
            {
                Logger.Log("\\cgreen*Running!");

                Logger.Log(await Status());
                await CrashDetection();
            };
        }

        public async Task CrashDetection()
        {
            if (Program.IntendedRestart) { return; }

            var botTesting = DiscordClient.GetChannel(853036595401981983) as SocketTextChannel;

            var logPath = new DirectoryInfo("logs").GetFiles().OrderBy(p => p.CreationTime).ToArray()[^2].FullName;

            await botTesting.SendFileAsync(logPath, "Crash detected, <@126481324017057792>");
        }

        public void SetupTelegramBot()
        {
            var token = Config["telegramToken"].Value<string>();
            TelegramClient = new TelegramBotClient(token);
            TelegramClient.OnMessage += TelegramClient_OnMessage;
        }

        private async void TelegramClient_OnMessage(object sender, Telegram.Bot.Args.MessageEventArgs e)
        {
            if (e.Message.Text?.ToLower() == "/chatid")
            {
                await TelegramClient.SendTextMessageAsync(
                    e.Message.Chat.Id,
                    $"This channel's ID: {e.Message.Chat.Id}\nThat message's ID: {e.Message.MessageId}",
                    replyToMessageId: e.Message.MessageId);
            }
        }

        public async Task Exit()
        {
            await DiscordClient.StopAsync();
        }

        public async Task<string> Status()
        {
            var linkText = "";
            foreach (var l in ActiveLinks)
            {
                linkText += $"#{l.GetDiscordChannel()?.Name} ({l.DiscordChannelId}) <-> " +
                    $"{(await TelegramClient.GetChatAsync(l.GetTelegramGroup()))?.Title} ({l.TelegramGroupId})\n";
            }
            linkText = linkText.Trim();

            var statusMessage = @$"
||------------------------------------------------------------------------------------||
\cblue*Discord Status:  \cwhite*{DiscordClient.ConnectionState} / {DiscordClient.LoginState}
\ccyan*Telegram Status: \cwhite*(IsReceiving: {TelegramClient.IsReceiving}) / (ApiTest: {await TelegramClient.TestApiAsync()})
\ccyan*Links:\cwhite*
{linkText}
||------------------------------------------------------------------------------------||";
            return statusMessage;
        }
    }
}
