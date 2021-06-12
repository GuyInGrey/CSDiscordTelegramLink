using System;
using System.IO;

namespace CSDiscordTelegramLink
{
    class Program
    {
        static void Main(string[] args)
        {
            Extensions.Log("Hello, world!");
            Extensions.Log(@"Color tests:
\bwhite*\cblack*Black
\cdarkblue*DarkBlue
\cdarkcyan*DarkCyan
\cdarkgray*DarkGray
\cdarkgreen*DarkGreen
\cdarkmagenta*DarkMagenta
\cdarkred*DarkRed
\cdarkyellow*DarkYellow\bblack*
\cblue*Blue
\ccyan*Cyan
\cgray*Gray
\cgreen*Green
\cmagenta*Magenta
\cred*Red
\cwhite*White
\cyellow*Yellow".Replace("\n", " ").Replace("\r", ""));
            if (args.Length <= 0 || !Directory.Exists(args[0]))
            {
                if (args.Length > 0)
                {
                    Extensions.Log(args[0]);
                }

                Extensions.Log(Directory.GetCurrentDirectory());
                Extensions.Log("Invalid arguments.");
                return;
            }

            Directory.SetCurrentDirectory(args[0]);

            var bot = new BotManager();
            while (true)
            {
                var text = Console.ReadLine().ToLower();
                if (text == "exit")
                {
                    Extensions.Log("Exiting...");
                    bot.Exit();
                    Extensions.Log("Goodbye!");
                    break;
                }
            }
        }
    }
}
