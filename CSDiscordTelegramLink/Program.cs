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

            var bot = new BotManager();
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
                else
                {
                    Logger.Log("Unknown command. The only ones are `exit` and `status` at the moment.");
                }
            }
        }

        private static void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            var e2 = e.ExceptionObject;
            Logger.HandleObject(e2);
            Environment.Exit(1);
        }
    }
}
