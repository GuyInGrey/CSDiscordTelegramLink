using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using MySql.Data.MySqlClient;

namespace CSDiscordTelegramLink
{
    public class DTMessage
    {
        public long TelegramGroupId;
        public int TelegramMessageId;
        public ulong DiscordMessageId;
        public DTOrigin Origin;

        public static DTMessage FromReader(MySqlDataReader reader)
        {
            if (!reader.HasRows) { return null; }
            reader.Read();

            return new DTMessage()
            {
                TelegramGroupId = long.Parse(reader["telegramgroup"].ToString()),
                TelegramMessageId = int.Parse(reader["telegrammessage"].ToString()),
                DiscordMessageId = ulong.Parse(reader["discordmessage"].ToString()),
                Origin = (DTOrigin)int.Parse(reader["origin"].ToString()),
            };
        }

        public enum DTOrigin
        {
            Discord = 0,
            Telegram = 1,
            Tag = 2,
        }
    }
}
