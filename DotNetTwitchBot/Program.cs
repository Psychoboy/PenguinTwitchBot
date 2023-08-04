global using System.ComponentModel.DataAnnotations;
global using System.ComponentModel.DataAnnotations.Schema;
global using Microsoft.EntityFrameworkCore;
global using DotNetTwitchBot.Bot.Models;
global using DotNetTwitchBot.Bot.Core.Database;
using Quartz;
using DotNetTwitchBot.Bot.Core;
using Serilog;
using TwitchLib.EventSub.Websockets.Extensions;

using DotNetTwitchBot.Bot.TwitchServices;

internal class Program
{
    private static async Task Main(string[] args)
    {

        var builder = WebApplication.CreateBuilder(args);
        var section = builder.Configuration.GetSection("Secrets");
        var secretsFileLocation = section.GetValue<string>("SecretsConf");
        if (secretsFileLocation == null) throw new Exception("Invalid file configuration");
        builder.Configuration.AddJsonFile(secretsFileLocation);

        builder.Host.ConfigureLogging((context, loggingBuilder) =>
        {
            var logger = new LoggerConfiguration()
            .ReadFrom.Configuration(builder.Configuration)
            .Enrich.FromLogContext();
            loggingBuilder.ClearProviders();
            loggingBuilder.AddSerilog(logger.CreateLogger());
        });
        // Add services to the container.
        builder.Services.AddControllersWithViews();
        builder.Services.AddSingleton<SettingsFileManager>();
        builder.Services.AddSingleton<ServiceBackbone>();
        builder.Services.AddSingleton<TwitchService>();
        builder.Services.AddSingleton<TwitchBotService>();
        builder.Services.AddSingleton<DotNetTwitchBot.Bot.Commands.CommandHandler>();
        // TODO: builder.Services.AddSingleton<DiscordService>();

        builder.Services.AddRazorPages();
        builder.Services.AddServerSideBlazor();

        //Database
        builder.Services.AddSingleton<IDatabaseTools, DatabaseTools>();

        builder.Services.AddHostedService<TwitchChatBot>();
        builder.Services.AddTwitchLibEventSubWebsockets();
        builder.Services.AddHostedService<TwitchWebsocketHostedService>();
        builder.Services.AddSingleton<DotNetTwitchBot.Bot.Alerts.SendAlerts>();
        builder.Services.AddSingleton<DotNetTwitchBot.Bot.Notifications.IWebSocketMessenger, DotNetTwitchBot.Bot.Notifications.WebSocketMessenger>();
        builder.Services.AddSingleton<DotNetTwitchBot.Bot.Commands.Moderation.IKnownBots, DotNetTwitchBot.Bot.Commands.Moderation.KnownBots>();
        builder.Services.AddSingleton<DotNetTwitchBot.Bot.Core.SubscriptionTracker>();
        //builder.Services.AddSingleton<DotNetTwitchBot.Bot.Commands.Music.YtPlayer>();

        //Add Features Here:
        var commands = new List<Type>
        {
            typeof(DotNetTwitchBot.Bot.Commands.Features.ViewerFeature),
            typeof(DotNetTwitchBot.Bot.Commands.Features.TicketsFeature),
            typeof(DotNetTwitchBot.Bot.Commands.Features.GiveawayFeature),
            typeof(DotNetTwitchBot.Bot.Commands.Features.LoyaltyFeature),
            typeof(DotNetTwitchBot.Bot.Commands.TicketGames.WaffleRaffle),
            typeof(DotNetTwitchBot.Bot.Commands.TicketGames.PancakeRaffle),
            typeof(DotNetTwitchBot.Bot.Commands.TicketGames.BaconRaffle),
            typeof(DotNetTwitchBot.Bot.Commands.TicketGames.Roulette),
            typeof(DotNetTwitchBot.Bot.Commands.TicketGames.DuelGame),
            typeof(DotNetTwitchBot.Bot.Commands.TicketGames.ModSpam),
            typeof(DotNetTwitchBot.Bot.Commands.Misc.AddActive),
            typeof(DotNetTwitchBot.Bot.Commands.Misc.First),
            typeof(DotNetTwitchBot.Bot.Commands.Misc.DailyCounter),
            typeof(DotNetTwitchBot.Bot.Commands.Misc.DeathCounter),
            typeof(DotNetTwitchBot.Bot.Commands.Misc.LastSeen),
            typeof(DotNetTwitchBot.Bot.Commands.Misc.Top),
            typeof(DotNetTwitchBot.Bot.Commands.Misc.QuoteSystem),
            typeof(DotNetTwitchBot.Bot.Commands.Misc.RaidTracker),
            typeof(DotNetTwitchBot.Bot.Commands.Misc.Weather),
            typeof(DotNetTwitchBot.Bot.Commands.Misc.ShoutoutSystem),
            typeof(DotNetTwitchBot.Bot.Commands.Misc.Timers),
            typeof(DotNetTwitchBot.Bot.Commands.Custom.CustomCommand),
            typeof(DotNetTwitchBot.Bot.Commands.Custom.AudioCommands),
            typeof(DotNetTwitchBot.Bot.Commands.Custom.Alias),
            typeof(DotNetTwitchBot.Bot.Commands.PastyGames.Defuse),
            typeof(DotNetTwitchBot.Bot.Commands.PastyGames.Roll),
            typeof(DotNetTwitchBot.Bot.Commands.PastyGames.FFA),
            typeof(DotNetTwitchBot.Bot.Commands.PastyGames.Gamble),
            typeof(DotNetTwitchBot.Bot.Commands.PastyGames.Steal),
            typeof(DotNetTwitchBot.Bot.Commands.PastyGames.Heist),
            typeof(DotNetTwitchBot.Bot.Commands.PastyGames.Slots),
            typeof(DotNetTwitchBot.Bot.Commands.PastyGames.Tax),
            typeof(DotNetTwitchBot.Bot.Commands.Music.YtPlayer),
            typeof(DotNetTwitchBot.Bot.Commands.Moderation.Blacklist),
            typeof(DotNetTwitchBot.Bot.Commands.Moderation.Admin)
        };

        //Add Alerts
        builder.Services.AddSingleton<DotNetTwitchBot.Bot.Alerts.AlertImage>();

        foreach (var cmd in commands)
        {
            builder.Services.AddSingleton(cmd);
        }

        //Backup Jobs:
        builder.Services.AddSingleton<DotNetTwitchBot.Bot.ScheduledJobs.BackupDbJob>();

        builder.Services.AddQuartz(q =>
        {
            q.UseMicrosoftDependencyInjectionJobFactory();

            var backupDbJobKey = new JobKey("BackupDbJob");
            q.AddJob<DotNetTwitchBot.Bot.ScheduledJobs.BackupDbJob>(opts => opts.WithIdentity(backupDbJobKey));
            q.AddTrigger(opts => opts
                .ForJob(backupDbJobKey)
                .WithIdentity("BackupDb-Trigger")
                .WithCronSchedule(CronScheduleBuilder.DailyAtHourAndMinute(12, 00)) //Every day at noon
            );
            q.InterruptJobsOnShutdown = true;
        });
        builder.Services.AddQuartzHostedService(
            q => q.WaitForJobsToComplete = true
        );

        var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
        builder.Services.AddDbContext<ApplicationDbContext>(options =>
        {
            options.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString), options => options.EnableRetryOnFailure(
                maxRetryCount: 5,
                maxRetryDelay: System.TimeSpan.FromSeconds(15),
                errorNumbersToAdd: null
            ));
        });

        builder.Services.AddSignalR();


        builder.Configuration.GetRequiredSection("Discord").Get<DiscordSettings>();

        var app = builder.Build();

        if (!app.Environment.IsDevelopment())
        {
            using (var scope = app.Services.CreateScope())
            {
                var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                dbContext.Database.Migrate();
            }
        }
        //Loads all the command stuff into memory
        //app.Services.GetRequiredService<DotNetTwitchBot.Bot.Commands.RegisterCommands>();
        // TODO: app.Services.GetRequiredService<DotNetTwitchBot.Bot.Core.DiscordService>();

        await app.Services.GetRequiredService<DotNetTwitchBot.Bot.Commands.Moderation.IKnownBots>().LoadKnownBots();

        foreach (var cmd in commands)
        {
            var commandService = (DotNetTwitchBot.Bot.Commands.IBaseCommandService)app.Services.GetRequiredService(cmd);
            await commandService.Register();
        }


        await app.Services.GetRequiredService<IDatabaseTools>().Backup();

        app.UseMiddleware<DotNetTwitchBot.CustomMiddleware.ErrorHandlerMiddleware>();

        // Configure the HTTP request pipeline.
        if (!app.Environment.IsDevelopment())
        {
            app.UseExceptionHandler("/Home/Error");
            // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
            app.UseHsts();
        }

        app.UseHttpsRedirection();
        app.UseStaticFiles();

        app.UseRouting();


        app.UseAuthorization();

        var wsOptions = new WebSocketOptions
        {
            KeepAliveInterval = TimeSpan.FromMinutes(2)
        };
        app.UseWebSockets(wsOptions);

        app.MapControllerRoute(
            name: "default",
            pattern: "{controller=Home}/{action=Index}/{id?}");

        app.UseEndpoints(endpoints =>
        {
            endpoints.MapControllers();
        });

        var logger = app.Logger;
        var lifetime = app.Lifetime;
        lifetime.ApplicationStarted.Register(() =>
        {
            logger.LogInformation("Application Starting");
        });

        var websocketMessenger = app.Services.GetRequiredService<DotNetTwitchBot.Bot.Notifications.IWebSocketMessenger>();
        lifetime.ApplicationStopping.Register(async () =>
        {
            logger.LogInformation("Application trying to stop.");
            await websocketMessenger.CloseAllSockets();
        });
        AppDomain.CurrentDomain.FirstChanceException += (sender, eventArgs) =>
        {
            if (eventArgs.Exception.GetType() == typeof(System.Net.Sockets.SocketException) ||
                eventArgs.Exception.GetType() == typeof(System.IO.IOException) ||
                eventArgs.Exception.GetType() == typeof(System.Net.WebSockets.WebSocketException) ||
                eventArgs.Exception.GetType() == typeof(System.Threading.Tasks.TaskCanceledException) ||
                eventArgs.Exception.GetType() == typeof(Discord.WebSocket.GatewayReconnectException) ||
                eventArgs.Exception.GetType() == typeof(TwitchLib.Api.Core.Exceptions.InternalServerErrorException))
            {
                return; //Ignore
            }
            logger.LogDebug(eventArgs.Exception, "Global Exception Caught");
        };
        app.MapHub<DotNetTwitchBot.Bot.Commands.Music.YtHub>("/ythub");
        app.MapHub<DotNetTwitchBot.Bot.Hubs.GiveawayHub>("/giveawayhub");

        app.MapBlazorHub();
        app.MapFallbackToPage("/_Host");
        app.Run(); //Start in future to read input
    }
}