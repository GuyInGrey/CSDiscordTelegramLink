using System;
using System.Data;

using Discord;

using MySql.Data.MySqlClient;

using Telegram.Bot.Types;

namespace CSDiscordTelegramLink
{
    public static class ReplyManager
    {
        private static MySqlConnection Connection;
        private static DatabaseCredentials Credentials;

        public static bool Init(DatabaseCredentials creds)
        {
            Credentials = creds;
            var connected = CheckConnection();

            if (connected) { CreateTable(); }

            return connected;
        }

        private static void CreateTable()
        {
            var cmd = @"CREATE TABLE IF NOT EXISTS replyhistory (
  `telegramgroup` BIGINT(40) NULL,
  `telegrammessage` BIGINT(40) NULL,
  `discordmessage` BIGINT(40) NULL,
  `origin` SMALLINT(10) NULL);";

            var comm = new MySqlCommand(cmd, Connection);
            comm.ExecuteNonQuery();
        }

        private static bool CheckConnection()
        {
            if (Credentials is null)
            {
                Logger.Log("\\cred*Error: No database credentials defined on connection check!");
                return false;
            }

            if (Connection is not null && Connection.State == System.Data.ConnectionState.Open)
            {
                return true;
            }
            else if (Connection is not null)
            {
                Logger.Log("\\cred*Database is in non-open state.");
                Connection.Dispose();
            }

            Logger.Log("Attemping to open database connection...");
            try
            {
                Connection = new MySqlConnection(Credentials.GetConnectionString());
                Connection.Open();
                Logger.Log("Database connection opened.");
                return true;
            }
            catch (Exception e)
            {
                Logger.Log("\\cred*Failed to open database connection.");
                Logger.Log("\\cred*" + e.Message);
                Logger.Log("\\cred*" + e.StackTrace.Replace("\n", "\n\\cred*"));
                return false;
            }
        }

        /// <summary>
        /// Adds a reply structure to the database.
        /// </summary>
        /// <param name="origin">0 = Discord, 1 = Telegram</param>
        public static void Add(long telegramGroupId, long telegramMessageId, ulong discordMessageId, DTMessage.DTOrigin origin)
        {
            if (!CheckConnection()) { return; }
            var comm = new MySqlCommand("INSERT INTO replyhistory " +
                "(telegramgroup, telegrammessage, discordmessage, origin) " +
                "VALUES (@p0, @p1, @p2, @p3);", Connection);
            comm.Parameters.AddWithValue("@p0", telegramGroupId);
            comm.Parameters.AddWithValue("@p1", telegramMessageId);
            comm.Parameters.AddWithValue("@p2", discordMessageId);
            comm.Parameters.AddWithValue("@p3", (int)origin);
            comm.ExecuteNonQuery();
        }

        public static Optional<DTMessage> GetFromDiscord(ulong discordMessage)
        {
            var q = $"SELECT * FROM replyhistory WHERE discordmessage='{discordMessage}';";
            var cmd = new MySqlCommand(q, Connection);
            using var reader = cmd.ExecuteReader();
            if (!reader.HasRows)
            {
                return Optional.Create<DTMessage>();
            }
            return DTMessage.FromReader(reader);
        }

        public static Optional<DTMessage> GetFromTelegram(long telegramGroup, int telegramMessage)
        {
            var q = $"SELECT * FROM replyhistory WHERE telegramgroup='{telegramGroup}' AND telegrammessage='{telegramMessage}';";
            var cmd = new MySqlCommand(q, Connection);
            using var reader = cmd.ExecuteReader();
            if (!reader.HasRows)
            {
                return Optional.Create<DTMessage>();
            }
            return DTMessage.FromReader(reader);
        }
    }
}
