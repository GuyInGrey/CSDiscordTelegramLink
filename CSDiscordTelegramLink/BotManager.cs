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
            Console.WriteLine("Loading configuration...");
            Config = JObject.Parse(System.IO.File.ReadAllText(@"C:\ISLE\config.json"));

            // Reset temp files
            Console.WriteLine("Resetting temp directory...");
            if (Directory.Exists("temp"))
            {
                Directory.Delete("temp", true);
            }
            Directory.CreateDirectory("temp");

            // Startup
            Console.WriteLine("Starting up...");
            SetupTelegramBot();
            SetupDiscordBot().GetAwaiter().GetResult();

            Console.WriteLine("Creating links...");

            var avatarChannelId = ulong.Parse(Config["discordAvatarChannel"].Value<string>());
            var links = Config["links"].Value<JArray>();
            foreach (JObject linkToken in links)
            {
                var link = Link.FromJson(linkToken, avatarChannelId);
                ActiveLinks.Add(link);
            }

            Console.WriteLine("Activating links...");
            ActiveLinks.ForEach(l => l.Listen(DiscordClient, TelegramClient));

            Console.WriteLine("Running!");
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
            Console.WriteLine("Discord bot started.");
        }

        public void SetupTelegramBot()
        {
            var token = Config["telegramToken"].Value<string>();
            TelegramClient = new TelegramBotClient(token);
            TelegramClient.StartReceiving();
            Console.WriteLine("Telegram bot started.");
        }
    }
}
