using Newtonsoft.Json.Linq;

namespace CSDiscordTelegramLink
{
    public class DatabaseCredentials
    {
        public string Host;
        public int Port;
        public string Username;
        public string Password;
        public string Database;

        public static DatabaseCredentials FromJson(JObject o)
        {
            return new DatabaseCredentials()
            {
                Host = o["host"].Value<string>(),
                Port = o["port"].Value<int>(),
                Username = o["username"].Value<string>(),
                Password = o["password"].Value<string>(),
                Database = o["database"].Value<string>(),
            };
        }

        public string GetConnectionString() =>
                $"Server={Host}; " +
                $"Port={Port};" +
                $"Database={Database}; " +
                $"Uid={Username}; " +
                $"Pwd={Password};";
    }
}
