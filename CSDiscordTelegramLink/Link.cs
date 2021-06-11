﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
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

        // False = 1, True = 2
        private bool WhichWebhook;
        private string LastTelegramName = "";

        // (Telegram, Discord)
        private static List<(int, ulong)> MessageHistory = new();

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

            discord.MessageReceived += Discord_MessageReceived;
            telegram.OnMessage += Telegram_OnMessage;
        }

        private async Task Discord_MessageReceived(SocketMessage arg)
        {
            if (arg.Author.IsBot || 
                arg.Source == MessageSource.System ||
                arg.Channel.Id != DiscordChannelId) 
            { return; }

            var user = arg.Author.Username;
            var cleanContent = arg.Content;

            cleanContent = Regex.Replace(arg.Content, @"<:([^:]+):\d+>", m =>
            {
                if (m is null || m.Groups.Count < 2) { return ":?"; }
                return $":{m.Groups[1]}:";
            });

            cleanContent = Regex.Replace(cleanContent, @"<@!?(\d+)>", m =>
            {
                if (m is null || m.Groups.Count < 2) { return "@?"; }
                if (!ulong.TryParse(m.Groups[1].ToString(), out var id)) { return "@?"; }
                var user = DiscordBot.GetUser(id);
                if (user is null || user.Username is null) { return "@?"; }
                return "@" + user.Username;
            });

            cleanContent = Regex.Replace(cleanContent, @"<#!?(\d+)>", m =>
            {
                if (m is null || m.Groups.Count < 2) { return "#?"; }
                if (!ulong.TryParse(m.Groups[1].ToString(), out var id)) { return "#?"; }
                var channel = DiscordBot.GetChannel(id);
                if (channel is null) { return "#?"; }
                if (channel is not SocketTextChannel textChannel || textChannel.Name is null) { return "#?"; }
                return textChannel.Name;
            });

            cleanContent = Regex.Replace(cleanContent, @"([.*\-+!#(){}|><_])", m =>
            {
                if (m is null || m.Groups.Count < 2) { return ""; }
                return $"\\{m.Groups[1]}";
            });

            cleanContent = $"*__{user}__* \n{cleanContent}";

            var msg = await TelegramBot.SendTextMessageAsync(GetTelegramGroup(), cleanContent, Telegram.Bot.Types.Enums.ParseMode.MarkdownV2);
            MessageHistory.Add((msg.MessageId, arg.Id));

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
            //if ((new ChatId(e.Message.Chat.Id)) != TelegramGroup) { return; }

            Console.WriteLine(e.Message.Chat.Id);

            if (DiscordBot.LoginState != LoginState.LoggedIn ||
                DiscordBot.ConnectionState != ConnectionState.Connected ||
                e.Message.From.IsBot)
            { return; }

            var name = e.Message.From.FirstName + " " + e.Message.From.LastName;

            // Determine webhook
            if (LastTelegramName != name) { WhichWebhook ^= true; }
            LastTelegramName = name;
            var hookUrlToUse = WhichWebhook ? Webhook1 : Webhook2;
            using var hookToUse = new DiscordWebhookClient(hookUrlToUse);

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
                var linked = MessageHistory.FirstOrDefault(m => m.Item1 == replyId);
                if (linked != default)
                {
                    var url = $"https://discord.com/channels/{GetDiscordChannel().Guild.Id}/{DiscordChannelId}/{linked.Item2}";
                    text = $"[Reply <:cursor:852946682509262899>]({url})\n{text}";
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
            if (filePath is not null)
            {
                id = await hookToUse.SendFileAsync(
                    text: text,
                    username: name,
                    avatarUrl: avatarUrl,
                    filePath: filePath);
            }
            else
            {
                id = await hookToUse.SendMessageAsync(
                    text: text,
                    username: name,
                    avatarUrl: avatarUrl);
            }

            MessageHistory.Add((e.Message.MessageId, id));
        }
    }
}