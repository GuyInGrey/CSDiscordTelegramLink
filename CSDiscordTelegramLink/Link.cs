using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Discord;
using Discord.Rest;
using Discord.Webhook;
using Discord.WebSocket;

using Newtonsoft.Json.Linq;

using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.InputFiles;

namespace CSDiscordTelegramLink
{
    public class Link
    {
        public long TelegramGroupId;
        public ulong DiscordChannelId;
        public string Webhook1;
        public string Webhook2;

        private DiscordSocketClient DiscordBot;
        private TelegramBotClient TelegramBot;
        private ulong DiscordAvatarChannelId;
        private SocketTextChannel DiscordAvatarChannel => DiscordBot.GetChannel(DiscordAvatarChannelId) as SocketTextChannel;

        private DiscordWebhookClient Hook1;
        private DiscordWebhookClient Hook2;

        // False = 1, True = 2
        private bool WhichWebhook;
        private string LastTelegramName = "";

        private TicketManager TicketManager;

        public ChatId GetTelegramGroup() => 
            new(TelegramGroupId);

        public SocketTextChannel GetDiscordChannel() => 
            DiscordBot.GetChannel(DiscordChannelId) as SocketTextChannel;

        public static Link FromJson(JObject j, ulong avatarChannel)
        {
            return new Link()
            {
                TelegramGroupId = long.Parse(j["telegramGroupId"].Value<string>()),
                DiscordChannelId = ulong.Parse(j["discordChannelId"].Value<string>()),
                Webhook1 = j["discordWebhook1"].Value<string>(),
                Webhook2 = j["discordWebhook2"].Value<string>(),
                DiscordAvatarChannelId = avatarChannel,
            };
        }

        public void Listen(DiscordSocketClient discord, TelegramBotClient telegram, TicketManager manager)
        {
            DiscordBot = discord;
            TelegramBot = telegram;

            TicketManager = manager;

            Hook1 = new DiscordWebhookClient(Webhook1);
            Hook2 = new DiscordWebhookClient(Webhook2);

            discord.MessageReceived += Discord_MessageReceived;
            telegram.OnMessage += Telegram_OnMessage;
        }

        private async Task Discord_MessageReceived(SocketMessage arg)
        {
            if (arg.Author.IsWebhook || 
                arg.Source == MessageSource.System ||
                arg.Channel.Id != DiscordChannelId) 
            { return; }

            TicketManager.WaitForTurn();

            // Determine reply
            var replyToMessageId = 0;
            if (arg.Reference is not null && arg.Reference.MessageId.IsSpecified)
            {
                var val = ReplyManager.GetFromDiscord(arg.Reference.MessageId.Value);
                if (val.IsSpecified)
                {
                    replyToMessageId = val.Value.TelegramMessageId;
                }
            }

            // Determine content
            var cleanContent = DiscordBot.CleanDiscordMessage(arg);

            var msg = await TelegramBot.SendTextMessageAsync(
                chatId: GetTelegramGroup(), 
                text: cleanContent, 
                parseMode: Telegram.Bot.Types.Enums.ParseMode.MarkdownV2,
                replyToMessageId: replyToMessageId);
            ReplyManager.Add(msg.Chat.Id, msg.MessageId, arg.Id, DTMessage.DTOrigin.Discord);

            foreach (var a in arg.Attachments)
            {
                try
                {
                    await TelegramBot.SendDocumentAsync(GetTelegramGroup(), new InputOnlineFile(a.Url));
                }
                catch
                {
                    await arg.AddReactionAsync(new Emoji("📁"));
                }
            }
        }

        private async void Telegram_OnMessage(object sender, Telegram.Bot.Args.MessageEventArgs e)
        {
            while (DiscordBot.LoginState != LoginState.LoggedIn ||
                DiscordBot.ConnectionState != ConnectionState.Connected)
            {
                Thread.Sleep(1);
            }

            if (e.Message.From.IsBot ||
                e.Message.Chat.Id != TelegramGroupId)
            { return; }

            var name = e.Message.From.FirstName + " " + e.Message.From.LastName;

            TicketManager.WaitForTurn();

            // Determine webhook
            if (LastTelegramName != name) { WhichWebhook ^= true; }
            LastTelegramName = name;
            var hookToUse = WhichWebhook ? Hook1 : Hook2;

            // Determine avatar
            RestUserMessage toDelete;
            string avatarUrl;
            var avatars = (await TelegramBot.GetUserProfilePhotosAsync(e.Message.From.Id))?.Photos;
            if (avatars.Length == 0 || avatars[0].Length == 0)
            {
                toDelete = await DiscordAvatarChannel.SendFileAsync("unknown.png", "");
                avatarUrl = toDelete.Attachments.First().Url;
            }
            else
            {
                var avatarPath = await TelegramBot.DownloadTelegramFile(avatars[0][0].FileId);
                toDelete = await DiscordAvatarChannel.SendFileAsync(avatarPath, "");
                avatarUrl = toDelete.Attachments.First().Url;
            }
            if (toDelete is not null)
            {
                await toDelete.DeleteAsync();
            }

            // Determine text content
            var replyId = e.Message.ReplyToMessage?.MessageId;

            var text = (e.Message.Text is object && e.Message.Text != "") ? e.Message.Text :
                (e.Message.Caption is object && e.Message.Caption != "") ? e.Message.Caption : "";

            // Determine reply
            var missingReply = false;
            var embeds = new List<Embed>();
            var em = new EmbedBuilder()
                .WithColor(Color.Magenta)
                .WithTitle("Replies To");

            if (replyId is null) { goto noReply; }
            var val = ReplyManager.GetFromTelegram(e.Message.Chat.Id, (int)replyId);
            if (!val.IsSpecified) 
            {
                missingReply = true;
                goto noReply; 
            }

            var linked = val.Value.DiscordMessageId;
            var linkedMessage = await GetDiscordChannel()?.GetMessageAsync(linked);
            if (linkedMessage is null) { goto noReply; }

            var url = $"https://discord.com/channels/{GetDiscordChannel().Guild.Id}/{DiscordChannelId}/{linked}";

            if (linkedMessage.Content is not null && linkedMessage.Content.Trim() != "")
            {
                var con = linkedMessage.Content;
                con = con.Length > 50 ? con.Substring(0, 50) + "..." : con;
                em.Description = $"[{con}]({url})";
            }
            else
            {
                em.Description = $"[Attachment]({url})";
            }

            if (linkedMessage.Attachments.Count > 0)
            {
                em.ThumbnailUrl = linkedMessage.Attachments.First().Url;
            }

            em = em.WithFooter(new EmbedFooterBuilder()
            {
                Text = linkedMessage.Author.Username,
                IconUrl = linkedMessage.Author.GetAvatarUrl(),
            });
            embeds.Add(em.Build());

            noReply:;

            text = e.Message.Sticker is not null ? $"{e.Message.Sticker.Emoji}\n{text}" : text;

            // Determine file
            string filePath = null;
            try
            {
                if (e.Message.Photo is not null)
                {
                    filePath = await TelegramBot.DownloadTelegramFile(e.Message.Photo.Last().FileId);
                }
                else if (e.Message.Document is not null)
                {
                    filePath = await TelegramBot.DownloadTelegramFile(e.Message.Document.FileId);
                }
                else if (e.Message.Audio is not null)
                {
                    filePath = await TelegramBot.DownloadTelegramFile(e.Message.Audio.FileId);
                }
            } 
            catch
            {
                embeds.Add(new EmbedBuilder()
                    .WithColor(Color.Red)
                    .WithDescription("Message contained an attachment that was too large.")
                    .Build());
            }

            // Send
            ulong id = 0;
            text = text == "" ? null : text;
            if (filePath is not null)
            {
                id = await hookToUse.SendFileAsync(
                    text: text,
                    username: name,
                    avatarUrl: avatarUrl,
                    embeds: embeds,
                    filePath: filePath);
            }
            else if (text is not null || embeds.Count != 0)
            {
                id = await hookToUse.SendMessageAsync(
                    text: text,
                    username: name,
                    avatarUrl: avatarUrl,
                    embeds: embeds);
            }

            if (id == 0)
            {
                return;
            }

            if (missingReply)
            {
                var msg = await GetDiscordChannel()?.GetMessageAsync(id);
                if (msg is not null)
                {
                    await msg.AddReactionAsync(new Emoji("⚠"));
                }
            }
            ReplyManager.Add(e.Message.Chat.Id, e.Message.MessageId, id, DTMessage.DTOrigin.Telegram);
        }
    }
}
