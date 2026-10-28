### Scheduled Tasks & Quartz Cron

Tasks run on a background Quartz.NET scheduler using standard Quartz 6-part cron expressions:

```
Seconds  Minutes  Hours  Day-of-Month  Month  Day-of-Week
```

#### Common Examples
* `0 0 12 * * ?` &rarr; Every day at 12:00 PM (Noon)
* `0 0/30 * * * ?` &rarr; Every 30 minutes
* `0 0 9 ? * MON` &rarr; Every Monday at 9:00 AM

#### Emote Cache
The emote cache refreshes once daily at 3:00 AM by default and is also warmed shortly after startup. Cached emotes remain available while a refresh is running. Use **Refresh Emote Cache Now** in the scheduled jobs settings to fetch updates immediately without restarting the bot.

#### Run On Startup
When **Run On Startup** is checked, the cleanup job immediately runs once when the bot boots up, ensuring outdated data is pruned without waiting for the first scheduled cron trigger.

> [!TIP]
> **Cron Builder:**
> Use the **Interactive Cron Builder** tool directly below the schedule table to visually create and apply valid Quartz cron expressions to any target job.

