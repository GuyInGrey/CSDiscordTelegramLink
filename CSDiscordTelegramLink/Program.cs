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
            Extensions.Log("Hello, world!");
            Extensions.Log(@"Color tests:

\cwhite*\bblack*o\bdarkblue*o\bdarkcyan*o\bdarkgray*o\bdarkgreen*o\bdarkmagenta*o\bdarkred*o\bdarkyellow*o
\cblack*\bwhite*o\bblue*o\bcyan*o\bgray*o\bgreen*o\bmagenta*o\bred*o\byellow*o
");
            if (args.Length <= 0 || !Directory.Exists(args[0]))
            {
                Extensions.Log("Invalid path: " + (args.Length > 0 ? args[0] : ""));
                return;
            }

            Directory.SetCurrentDirectory(args[0]);
            Extensions.Log("Running in directory: " + Directory.GetCurrentDirectory());
            Extensions.Log("OS: " + Environment.OSVersion);
            Extensions.Log("Dotnet Version: " + Environment.Version + "\n");


            var bot = new BotManager();
            while (true)
            {
                var text = Console.ReadLine().ToLower();
                if (text == "exit")
                {
                    Extensions.Log("Exiting...");
                    bot.Exit().GetAwaiter().GetResult();
                    Extensions.Log("Goodbye!");
                    break;
                }
                else if (text == "status")
                {
                    Extensions.Log(bot.Status().GetAwaiter().GetResult());
                }
                else
                {
                    Extensions.Log("Unknown command. The only ones are `exit` and `status` at the moment.");
                }
            }
        }
    }
}
