# twitch_lurk_bot

This is a proof of concept bot that shows that twitch sub gifting is broken. 

# Purpose

This bot does nothing more than crawl the directory for channels to join, and then monitors the chat of every single channel it's in for chatters with the partner badges, whose chat it also joins.
It will never speak in any chat, all it does is lurk there (as the name suggests). It also has a built in logging functionality on how it discovered each channel and who gifted subs to the bot.
No data except usernames will be stored.

# How to use

Build the project, deploy it on a machine with Redis running, edit the config to your needs and run. Everything else is handled by the bot.
It requires a valid oauth token and a valid client-id for api requests (no special permissions needed).
