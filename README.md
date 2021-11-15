# CSDiscordTelegramLink
A C# linking bot between Telegram and Discord

### Self hosting instructions:

1. Clone/download the project and open it in `Visual Studio`.  
2. Publish the project to folder, targetting the platform that will be hosting the bot.  
3. On the platform that will host the bot, install `.NET 5.0`.  
4. On the platform that will host the bot, install a `MySql` server. In that server, create a new database. This guide won't show how to do that as it depends on the platform.  
5. Create a new folder that will contain the config and temp data for the bot (the `runtime folder`).  
6. At [Discord's Developer page](https://discord.com/developers/applications), create a new application. 
   * Name it, and set it's avatar. 
   * Go to the bot tab and create a bot. Copy and note the token.
7. Create an invite link for the bot using these permissions, filling in the client id with your application id: https://discordapi.com/permissions.html#2147863616  
8. Create a new Discord server for the purpose of managing the bot. In it:
   * Invite the bot using your new invite links.
   * Create 2 new channels. [Note both their IDs.](https://www.reddit.com/r/discordapp/comments/50thqr/finding_channel_id/d76ttv5/?utm_source=reddit&utm_medium=web2x&context=3)
      * One will report crashes (`bot testing channel`, you should have notifications on for this)
      * One will be used for temporary avatar uploads (`avatar upload channel`, you can/should mute this)
9. In Telegram, go to @BotFather. Create a new bot via `/newbot`.
   * Set it's name and username, following the instructions.
   * Note the token it gives you.
10. Run `/setprivacy`, select the bot you made, and set to DISABLED. This will allow the bot to read all messages, which is needed for the link.
11. Run `/setcommands`, select the bot you made, and send `chatid - Get's the group's ID.`
12: In the runtime folder, create a `config.json`, filling in the template. `discordStatus` is the Discord bot's "playing" status, it can be left blank or set to whatever.

```json
{
  "discordToken": "Insert the Discord bot token here",
  "discordAvatarChannel": "Insert the ID of the avatar upload channel here",
  "botTesting": "Insert the ID of the bot testing channel here",
  "telegramToken": "Insert the Telegram bot token here",
  "discordStatus": "",
  
  "globalTags": [
  ],
  
  "database": {
    "host": "127.0.0.1",
    "port": 3306,
    "username": "Insert the mysql server username here",
    "password": "Insert the mysql server password here",
    "database": "Insert the name of the database within the mysql server"
  },
  
  "links": [
  ]
}
```
13. In the runtime folder, add an `unknown.png` file. This is the image that is seen in Discord when a telegram user has no avatar. [This is the image I use.](https://imgur.com/a/DKEKlOn)
13. Run the bot, passing one argument, which is the path to the runtime folder.
   * It is recommended this is automated, and restarts when stopped automatically, as this bot can and will crash on you. It's not perfect.

### For every link you want to make between Telegram and Discord, do the following:

1. In the Discord server you want to link, invite the bot using the link you generated in the above step 7.
2. In the channel you want to link, create 2 webhooks. 
   * They can be named whatever you want (I use `telegram1` and `telegram2`) and the avatars can be left blank.
   * Copy and note both of their webhook URLs.
   * Note the channel ID.
3. In Telegram, go to @BotFather. Run `/setjoingroups` and set it to `ENABLED` for your bot.
4. Invite the telegram bot to the group you want to link.
5. In Telegram, go to @BotFather. Run `/setjoingroups` and set it to `DISABLED` for your bot.
6. Stop and restart the bot process.
7. In the telegram group you want to link, run `/chatid@YourBotNameHere`.
   * Note the returned ID. It may include a `-`, that should be included in your note.
8. In your bot's `config.json`, there is a `links` array. In that array, insert the following object:
```json
{
  "telegramGroupId": "Insert telegram group id here (gotten from /chatid)",
  "discordChannelId": "Insert discord channel ID here",
  "discordWebhook1": "Insert first webhook URL here",
  "discordWebhook2": "Insert second webhook URL here",
  "tags": [
  ]
}
```
9. Stop and restart the bot process. The link is now complete.

### To insert tags into a link or the global tags:
1. In your bot's `config.json`, insert the following object into a tag array:
```json
{
  "name": "Insert the name of the tag here. This is what people will run when they want the response",
  "content": "Insert the response content here",
  "attachment": "Insert the attachment URL here",
}
```
   * You can have either the content or the attachment, or both, but not neither. Both fields must be there, if you don't want one, leave the value blank (`""`).
