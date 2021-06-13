using System;
using System.Data;

using MySql.Data.MySqlClient;

namespace CSDiscordTelegramLink
{
    public static class ReplyManager
    {
        private static MySqlConnection Connection;
        private static DatabaseCredentials Credentials;

        public static bool Init(DatabaseCredentials creds)
        {
            Credentials = creds;
            return CheckConnection();
        }

        private static bool CheckConnection()
        {
            if (Credentials is null)
            {
                Extensions.Log("\\cred*Error: No database credentials defined on connection check!");
                return false;
            }

            if (Connection is not null && Connection.State == ConnectionState.Open)
            {
                return true;
            }
            else if (Connection is not null)
            {
                Extensions.Log("\\cred*Database is in non-open state.");
                Connection.Dispose();
            }

            Extensions.Log("Attemping to open database connection...");
            try
            {
                Connection = new MySqlConnection(Credentials.GetConnectionString());
                Connection.OpenAsync();
                Extensions.Log("Database connection opened.");
                return true;
            }
            catch (Exception e)
            {
                Extensions.Log("\\cred*Failed to open database connection.");
                Extensions.Log("\\cred*" + e.Message);
                Extensions.Log("\\cred*" + e.StackTrace);
                return false;
            }
        }
    }
}
