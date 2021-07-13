using System;
using System.IO;

namespace CSDiscordTelegramLink
{
    class Program
    {
        // Hello there!
        // Thank you for looking through the source code :D
        // Hopefully it's not too messy.
        // Enjoy!

        static BotManager bot;

        static void Main(string[] args)
        {
            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;

            if (args.Length <= 0 || !Directory.Exists(args[0]))
            {
                Console.WriteLine("Invalid path: " + (args.Length > 0 ? args[0] : ""));
                return;
            }

            Directory.SetCurrentDirectory(args[0]);
            Logger.Init();

            Logger.Log("Hello, world!");
            Logger.LogDebug();

            bot = new BotManager();
            while (true)
            {
                var text = Console.ReadLine().Trim().ToLower();
                if (text == "exit")
                {
                    Logger.Log("Exiting...");
                    bot.Exit().GetAwaiter().GetResult();
                    Logger.Log("Goodbye!");
                    break;
                }
                else if (text == "status")
                {
                    Logger.Log(bot.Status().GetAwaiter().GetResult());
                }
                else if (text == "crash")
                {
                    Logger.Log("Forcing a crash.");
                    throw new Exception("Console-induced crash.");
                }
                else
                {
                    Logger.Log("Unknown command. The only ones are `exit`, `status`, and `crash`.");
                }
            }
        }

        private static void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            Logger.Log(e.ExceptionObject);

            try
            {
                Logger.Log("Exiting...");
                bot?.Exit()?.GetAwaiter().GetResult();
                Logger.Log("Goodbye!");
            }
            catch (Exception e2)
            {
                Logger.Log(e2);
            }

            while (Logger.HasQueued && 
                (bot is null || bot.DiscordClient is null || bot.DiscordClient.ConnectionState != Discord.ConnectionState.Disconnected)) 
            { }
            Environment.Exit(1);
        }
    }
}
