using System;

namespace CSDiscordTelegramLink
{
    class Program
    {
        static void Main()
        {
            _ = new BotManager();
            while (true)
            {
                var text = Console.ReadLine().ToLower();
                if (text == "exit") { break; }
            }
        }
    }
}
