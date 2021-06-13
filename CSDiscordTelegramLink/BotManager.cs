using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

using Discord;
using Discord.WebSocket;

using Newtonsoft.Json.Linq;

using Telegram.Bot;

namespace CSDiscordTelegramLink
{
    public class BotManager
    {
        public JObject Config;

        public DiscordSocketClient DiscordClient;
        public TelegramBotClient TelegramClient;
        public List<Link> ActiveLinks = new();

        public BotManager()
        {
            Extensions.Log("Starting up...");

            // Load config
            Extensions.Log("Loading configuration...");
            Config = JObject.Parse(File.ReadAllText("config.json"));
            Extensions.Log("Configuration loaded.");

            // Reset temp files
            Extensions.Log("Resetting temp directory...");
            if (Directory.Exists("temp"))
            {
                Directory.Delete("temp", true);
            }
            Directory.CreateDirectory("temp");
            Extensions.Log("Temp directory reset.");

            // Database connection
            var dbLoaded = ReplyManager.Init(DatabaseCredentials.FromJson(Config["database"] as JObject));
            if (!dbLoaded)
            {
                Extensions.Log("Failed to connect to database, aborting.");
                Environment.Exit(1);
            }

            // Startup
            SetupTelegramBot();
            SetupDiscordBot().GetAwaiter().GetResult();

            Extensions.Log("Creating links...");

            var avatarChannelId = ulong.Parse(Config["discordAvatarChannel"].Value<string>());
            var messageHistoryFile = Config["messageHistoryFile"].Value<string>();
            var links = Config["links"].Value<JArray>();
            foreach (JObject linkToken in links)
            {
                var link = Link.FromJson(linkToken, avatarChannelId, messageHistoryFile);
                ActiveLinks.Add(link);
            }

            Extensions.Log("Activating links...");
            ActiveLinks.ForEach(l => l.Listen(DiscordClient, TelegramClient));
        }

        public async Task SetupDiscordBot()
        {
            var discordConfig = new DiscordSocketConfig()
            {
                DefaultRetryMode = RetryMode.AlwaysRetry,
            };
            DiscordClient = new DiscordSocketClient(discordConfig);

            DiscordClient.Log += (msg) =>
            {
                var prefix = @"\cblue*[Discord]\cwhite* ";
                Extensions.Log(prefix + msg.Message);
                if (msg.Exception is not null)
                {
                    Extensions.Log($"{prefix}\\cred*{msg.Exception.Message}");
                    Extensions.Log($"{prefix}\\cred*{msg.Exception.StackTrace}");
                }

                return Task.CompletedTask;
            };
            DiscordClient.Ready += async () =>
            {
                Extensions.Log("\\cgreen*Running!");

                Extensions.Log(await Status());
            };

            var token = Config["discordToken"].Value<string>();
            await DiscordClient.LoginAsync(TokenType.Bot, token);

            await DiscordClient.StartAsync();

            var status = Config["discordStatus"].Value<string>();
            if (status != "")
            {
                await DiscordClient.SetGameAsync(status);
            }
            Extensions.Log("Discord bot started.");
        }

        public void SetupTelegramBot()
        {
            var token = Config["telegramToken"].Value<string>();
            TelegramClient = new TelegramBotClient(token);
            TelegramClient.StartReceiving();
            Extensions.Log("Telegram bot started.");
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
