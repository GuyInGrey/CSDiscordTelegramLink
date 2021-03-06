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

        public List<Tag> Tags;

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
                Tags = j["tags"].Value<JArray>().ToObject<List<Tag>>(),
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
            if (arg.Author.IsBot || 
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

            await OnMessage(arg.Content);
        }

        private async void Telegram_OnMessage(object sender, Telegram.Bot.Args.MessageEventArgs e)
        {
            Logger.Log("Message.");

            while (DiscordBot.LoginState != LoginState.LoggedIn ||
                DiscordBot.ConnectionState != ConnectionState.Connected)
            {
                Thread.Sleep(1);
            }

            if (e.Message.From.IsBot ||
                e.Message.Chat.Id != TelegramGroupId)
            { return; }

            new Thread(() =>
            {
                _ = ManageTelegramMsg(e.Message);
            }).Start();
        }

        private bool Cleared;

        public void ClearQueued()
        {
            Cleared = true;
            TicketManager.Tickets.Clear();
            new Thread(() =>
            {
                Thread.Sleep(10000);
                Cleared = false;
            }).Start();
        }

        private async Task ManageTelegramMsg(Message msg)
        {
            TicketManager.WaitForTurn();
            if (Cleared) { 
                return; 
            }

            var text = (msg.Text is object && msg.Text != "") ? msg.Text :
                (msg.Caption is object && msg.Caption != "") ? msg.Caption : "";

            var name = msg.From.FirstName + " " + msg.From.LastName;

            // Determine webhook
            if (LastTelegramName != name) { WhichWebhook ^= true; }
            LastTelegramName = name;
            var hookToUse = WhichWebhook ? Hook1 : Hook2;

            // Determine avatar
            RestUserMessage toDelete;
            string avatarUrl;
            var photos = (await TelegramBot.GetUserProfilePhotosAsync(msg.From.Id))?.Photos;
            if (photos.Length == 0 || photos[0].Length == 0)
            {
                toDelete = await DiscordAvatarChannel.SendFileAsync("unknown.png", "");
                avatarUrl = toDelete.Attachments.First().Url;
            }
            else
            {
                var avatarPath = await TelegramBot.DownloadTelegramFile(photos[0][0].FileId);
                toDelete = await DiscordAvatarChannel.SendFileAsync(avatarPath, "");
                avatarUrl = toDelete.Attachments.First().Url;
            }
            if (toDelete is not null)
            {
                await toDelete.DeleteAsync();
            }

            // Determine text content
            var replyId = msg.ReplyToMessage?.MessageId;

            // Determine reply
            var missingReply = false;
            var embeds = new List<Embed>();
            var em = new EmbedBuilder()
                .WithColor(Color.Magenta)
                .WithTitle("Replies To");

            if (replyId is null) { goto noReply; }
            var val = ReplyManager.GetFromTelegram(msg.Chat.Id, (int)replyId);
            if (!val.IsSpecified)
            {
                missingReply = true;
                goto noReply;
            }

            var linked = val.Value.DiscordMessageId;
            var linkmsg = await GetDiscordChannel()?.GetMessageAsync(linked);
            if (linkmsg is null) { goto noReply; }

            var url = $"https://discord.com/channels/{GetDiscordChannel().Guild.Id}/{DiscordChannelId}/{linked}";

            if (linkmsg.Content is not null && linkmsg.Content.Trim() != "")
            {
                var con = linkmsg.Content;
                con = con.Length > 50 ? con.Substring(0, 50) + "..." : con;
                em.Description = $"[{con}]({url})";
            }
            else
            {
                em.Description = $"[Attachment]({url})";
            }

            if (linkmsg.Attachments.Count > 0)
            {
                em.ThumbnailUrl = linkmsg.Attachments.First().Url;
            }

            em = em.WithFooter(new EmbedFooterBuilder()
            {
                Text = linkmsg.Author.Username,
                IconUrl = linkmsg.Author.GetAvatarUrl(),
            });
            embeds.Add(em.Build());

            noReply:;

            text = msg.Sticker is not null ? $"{msg.Sticker.Emoji}\n{text}" : text;

            // Determine file
            string filePath = null;
            try
            {
                if (msg.Photo is not null)
                {
                    filePath = await TelegramBot.DownloadTelegramFile(msg.Photo.Last().FileId);
                }
                else if (msg.Document is not null)
                {
                    filePath = await TelegramBot.DownloadTelegramFile(msg.Document.FileId);
                }
                else if (msg.Audio is not null)
                {
                    filePath = await TelegramBot.DownloadTelegramFile(msg.Audio.FileId);
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
            var textGroups = text.FormatSplit(2000, '\n');
            if (filePath is not null)
            {
                id = await hookToUse.SendFileAsync(
                    text: text,
                    username: name,
                    avatarUrl: avatarUrl,
                    embeds: embeds,
                    filePath: filePath);
            }
            else if (textGroups.Count > 0 || embeds.Count != 0)
            {
                id = await hookToUse.SendMessageAsync(
                    text: textGroups[0],
                    username: name,
                    avatarUrl: avatarUrl,
                    embeds: embeds);
            }
            if (textGroups.Count > 0) { textGroups.RemoveAt(0); }

            while (textGroups.Count > 0)
            {
                await hookToUse.SendMessageAsync(
                    text: textGroups[0],
                    username: name,
                    avatarUrl: avatarUrl);
                textGroups.RemoveAt(0);
            }

            await OnMessage(text);

            if (id == 0)
            {
                return;
            }

            if (missingReply)
            {
                var msg2 = await GetDiscordChannel()?.GetMessageAsync(id);
                if (msg2 is not null)
                {
                    await msg2.AddReactionAsync(new Emoji("⚠"));
                }
            }
            ReplyManager.Add(msg.Chat.Id, msg.MessageId, id, DTMessage.DTOrigin.Telegram);
        }

        private async Task OnMessage(string content)
        {
            if (content is null || !content.StartsWith("/")) { return; }
            var tagName = content[1..];
            if (tagName.Contains(" ")) { tagName = tagName.Split(' ')[0]; }

            var tags = BotManager.Config["globalTags"].Value<JArray>().ToObject<List<Tag>>().Concat(Tags);
            var match = tags.FirstOrDefault(t => t.Name.ToLower() == tagName.ToLower());
            if (match is null) { return; }

            await SendToBoth(match.Content, match.Attachment);
        }

        private async Task SendToBoth(string content = null, string attachment = null)
        {
            if (content is not null && content.Trim() != "")
            {
                TicketManager.WaitForTurn();
                var dId = (await GetDiscordChannel().SendMessageAsync(text: content)).Id;
                var tId = (await TelegramBot.SendTextMessageAsync(TelegramGroupId, content)).MessageId;
                ReplyManager.Add(TelegramGroupId, tId, dId, DTMessage.DTOrigin.Tag);
            }
            if (attachment is not null && attachment.Trim() != "")
            {
                TicketManager.WaitForTurn();
                var dId = (await GetDiscordChannel().SendMessageAsync(text: attachment)).Id;
                var tId = (await TelegramBot.SendTextMessageAsync(TelegramGroupId, attachment)).MessageId;
                ReplyManager.Add(TelegramGroupId, tId, dId, DTMessage.DTOrigin.Tag);
            }
        }
    }
}
