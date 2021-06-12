using System;
using System.Collections.Generic;
using System.IO;
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
            // Load config
            Extensions.Log("Loading configuration...");
            Config = JObject.Parse(File.ReadAllText("config.json"));

            // Reset temp files
            Extensions.Log("Resetting temp directory...");
            if (Directory.Exists("temp"))
            {
                Directory.Delete("temp", true);
            }
            Directory.CreateDirectory("temp");

            // Startup
            Extensions.Log("Starting up...");
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

            Extensions.Log("Running!");
        }

        public async Task SetupDiscordBot()
        {
            var discordConfig = new DiscordSocketConfig()
            {
                DefaultRetryMode = RetryMode.AlwaysRetry,
            };
            DiscordClient = new DiscordSocketClient(discordConfig);

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

        public void Exit()
        {
            DiscordClient.StopAsync();
        }
    }
}
