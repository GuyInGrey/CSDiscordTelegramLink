using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using Discord;
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

        public void Listen(DiscordSocketClient discord, TelegramBotClient telegram)
        {
            DiscordBot = discord;
            TelegramBot = telegram;

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
                    await arg.AddReactionAsync(new Emoji("⚠"));
                }
            }
        }

        private async void Telegram_OnMessage(object sender, Telegram.Bot.Args.MessageEventArgs e)
        {
            if (e.Message.Chat.Id != TelegramGroupId) { return; }

            if (DiscordBot.LoginState != LoginState.LoggedIn ||
                DiscordBot.ConnectionState != ConnectionState.Connected ||
                e.Message.From.IsBot)
            { return; }

            var name = e.Message.From.FirstName + " " + e.Message.From.LastName;

            // Determine webhook
            if (LastTelegramName != name) { WhichWebhook ^= true; }
            LastTelegramName = name;
            var hookToUse = WhichWebhook ? Hook1 : Hook2;

            // Determine avatar
            var avatarUrl = "";
            var avatars = (await TelegramBot.GetUserProfilePhotosAsync(e.Message.From.Id))?.Photos;
            if (avatars.Length == 0 || avatars[0].Length == 0)
            {
                avatarUrl = (await DiscordAvatarChannel.SendFileAsync("unknown.png", "")).Attachments.First().Url;
            }
            else
            {
                var avatarPath = await TelegramBot.DownloadTelegramFile(avatars[0][0].FileId);
                avatarUrl = (await DiscordAvatarChannel.SendFileAsync(avatarPath, "")).Attachments.First().Url;
            }

            // Determine text content and reply
            var replyId = e.Message.ReplyToMessage?.MessageId;

            var text = (e.Message.Text is object && e.Message.Text != "") ? e.Message.Text :
                (e.Message.Caption is object && e.Message.Caption != "") ? e.Message.Caption : "";

            if (replyId != default)
            {
                var val = ReplyManager.GetFromTelegram(e.Message.Chat.Id, (int)replyId);
                if (val.IsSpecified)
                {
                    var linked = val.Value.DiscordMessageId;
                    var url = $"https://discord.com/channels/{GetDiscordChannel().Guild.Id}/{DiscordChannelId}/{linked}";

                    var linkedMessage = await GetDiscordChannel()?.GetMessageAsync(linked);
                    if (linkedMessage is null ||
                        linkedMessage.Content is null ||
                        linkedMessage.Content == "")
                    {
                        text = $"[Reply <:cursor:852946682509262899>]({url})\n{text}";
                    }
                    else
                    {
                        var content = linkedMessage.Content;
                        if (content.StartsWith("[Reply") || content.StartsWith("> ["))
                        {
                            var lines = content.Split('\n').ToList();
                            lines.RemoveAt(0);
                            content = string.Join("\n", lines);
                        }

                        content = content.Replace("\n", "").Replace("\r", "");

                        content = content.Length > 100 ?
                            content.Substring(0, 97) + "..." :
                            content;
                        text = $"> [{content}]({url})\n{text}";
                    }
                }
            }
            text = e.Message.Sticker is not null ? $"{e.Message.Sticker.Emoji}\n{text}" : text;

            // Determine file
            string filePath = null;
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

            // Send
            ulong id = 0;
            text = text == "" ? null : text;
            if (filePath is not null)
            {
                id = await hookToUse.SendFileAsync(
                    text: text,
                    username: name,
                    avatarUrl: avatarUrl,
                    filePath: filePath);
            }
            else if (text is not null)
            {
                id = await hookToUse.SendMessageAsync(
                    text: text,
                    username: name,
                    avatarUrl: avatarUrl);
            }

            if (id == 0)
            {
                return;
            }

            ReplyManager.Add(e.Message.Chat.Id, e.Message.MessageId, id, DTMessage.DTOrigin.Telegram);
        }
    }
}
