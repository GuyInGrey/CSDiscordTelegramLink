using System;
using System.IO;

namespace CSDiscordTelegramLink
{
    class Program
    {
        static void Main(string[] args)
        {
            if (args.Length <= 0 || !Directory.Exists(args[0]))
            {
                Console.WriteLine("Invalid arguments.");
                return;
            }

            Directory.SetCurrentDirectory(args[0]);

            _ = new BotManager();
            while (true)
            {
                var text = Console.ReadLine().ToLower();
                if (text == "exit") { break; }
            }
        }
    }
}
