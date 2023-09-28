global using DotNetTwitchBot.Bot.Core.Database;
global using DotNetTwitchBot.Bot.Models;
global using Microsoft.EntityFrameworkCore;
global using System.ComponentModel.DataAnnotations;
global using System.ComponentModel.DataAnnotations.Schema;
using Blazor.Analytics;
using DotNetTwitchBot.Bot.Core;
using DotNetTwitchBot.Bot.Repository;
using DotNetTwitchBot.Bot.Repository.Repositories;
using DotNetTwitchBot.Bot.TwitchServices;
using DotNetTwitchBot.Circuit;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Components.Server.Circuits;
using MudBlazor.Services;
using Prometheus;
using Prometheus.DotNetRuntime;
using Quartz;
using Serilog;
using TwitchLib.EventSub.Websockets.Extensions;

internal class Program
{
    private static async Task Main(string[] args)
    {
        using var server = new Prometheus.KestrelMetricServer(port: 4999);
        server.Start();
        var builder = WebApplication.CreateBuilder(args);
        var section = builder.Configuration.GetSection("Secrets");
        var secretsFileLocation = section.GetValue<string>("SecretsConf") ?? throw new Exception("Invalid file configuration");
        builder.Configuration.AddJsonFile(secretsFileLocation);

        var loggerConfiguration = new LoggerConfiguration()
           .ReadFrom.Configuration(builder.Configuration)
           .Enrich.FromLogContext();
        builder.Logging.ClearProviders();
        builder.Logging.AddSerilog(loggerConfiguration.CreateLogger());

        // Add services to the container.
        builder.Services.AddControllersWithViews();
        builder.Services.AddSingleton<SettingsFileManager>();
        builder.Services.AddSingleton<ILanguage, Language>();
        builder.Services.AddSingleton<IServiceBackbone, ServiceBackbone>();
        builder.Services.AddSingleton<ITwitchService, TwitchService>();
        builder.Services.AddSingleton<TwitchBotService>();
        builder.Services.AddSingleton<DotNetTwitchBot.Bot.Commands.ICommandHandler, DotNetTwitchBot.Bot.Commands.CommandHandler>();
        builder.Services.AddSingleton<DiscordService>();

        builder.Services.AddSingleton<Leaderboards>();

        builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie();

        builder.Services.AddRazorPages();
        builder.Services.AddServerSideBlazor();
        builder.Services.AddMudServices();

        if (builder.Configuration["AnalyticsId"] != null)
        {
            builder.Services.AddGoogleAnalytics(builder.Configuration["AnalyticsId"]);
        }

        //Database
        builder.Services.AddSingleton<IDatabaseTools, DatabaseTools>();

        builder.Services.AddSingleton<TwitchChatBot>();
        builder.Services.AddTwitchLibEventSubWebsockets();
        builder.Services.AddHostedService<TwitchWebsocketHostedService>();
        builder.Services.AddSingleton<DotNetTwitchBot.Bot.Alerts.ISendAlerts, DotNetTwitchBot.Bot.Alerts.SendAlerts>();
        builder.Services.AddSingleton<DotNetTwitchBot.Bot.Notifications.IWebSocketMessenger, DotNetTwitchBot.Bot.Notifications.WebSocketMessenger>();
        builder.Services.AddSingleton<DotNetTwitchBot.Bot.Commands.Moderation.IKnownBots, DotNetTwitchBot.Bot.Commands.Moderation.KnownBots>();
        builder.Services.AddSingleton<DotNetTwitchBot.Bot.Core.SubscriptionTracker>();

        RegisterDbServices(builder.Services);


        //Add Features Here:
        var commands = new List<Type>
        {
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
            typeof(DotNetTwitchBot.Bot.Commands.Misc.DeathCounters),
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
            typeof(DotNetTwitchBot.Bot.Commands.Moderation.Admin),
            typeof(DotNetTwitchBot.Bot.Commands.Metrics.SongRequests),
            typeof(DotNetTwitchBot.Bot.Commands.Moderation.BannedUsers)
        };
        builder.Services.AddSingleton<DotNetTwitchBot.Bot.Commands.PastyGames.MaxBetCalculator>();
        //Add Alerts
        builder.Services.AddSingleton<DotNetTwitchBot.Bot.Alerts.AlertImage>();

        foreach (var cmd in commands)
        {
            builder.Services.AddSingleton(cmd);
        }

        commands.AddRange(RegisterCommandServices(builder.Services));

        //Backup Jobs:
        builder.Services.AddSingleton<DotNetTwitchBot.Bot.ScheduledJobs.BackupDbJob>();

        builder.Services.AddQuartz(q =>
        {
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

        builder.Services.AddSingleton<ICircuitUserService, CircuitUserService>();
        builder.Services.AddScoped<CircuitHandler>((sp) =>
            new CircuitHandlerService(sp.GetRequiredService<ICircuitUserService>()));


        builder.Configuration.GetRequiredSection("Discord").Get<DiscordSettings>();



        var app = builder.Build();
        app.UseAuthentication();
        app.UseAuthorization();

        if (!app.Environment.IsDevelopment())
        {
            using var scope = app.Services.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            dbContext.Database.Migrate();
        }
        //Loads all the command stuff into memory
        app.Services.GetRequiredService<DotNetTwitchBot.Bot.Core.DiscordService>();

        await app.Services.GetRequiredService<DotNetTwitchBot.Bot.Commands.Moderation.IKnownBots>().LoadKnownBots();

        foreach (var cmd in commands)
        {
            var commandService = (DotNetTwitchBot.Bot.Commands.IBaseCommandService)app.Services.GetRequiredService(cmd);
            await commandService.Register();
        }

        await app.Services.GetRequiredService<IDatabaseTools>().Backup();
        await app.Services.GetRequiredService<TwitchChatBot>().Initialize();

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

        app.MapControllers();
        app.UseHttpMetrics();
        var collector = DotNetRuntimeStatsBuilder.Default().StartCollecting();

        var logger = app.Logger;
        var lifetime = app.Lifetime;
        lifetime.ApplicationStarted.Register(() =>
        {
            logger.LogInformation("Application Starting");
        });

        if (builder.Configuration["AnalyticsId"] != null)
        {
            logger.LogInformation("AnalyticsId found so analytics started.");
        }

        var websocketMessenger = app.Services.GetRequiredService<DotNetTwitchBot.Bot.Notifications.IWebSocketMessenger>();
        lifetime.ApplicationStopping.Register(async () =>
        {
            logger.LogInformation("Application trying to stop.");
            await websocketMessenger.CloseAllSockets();
        });
        AppDomain.CurrentDomain.FirstChanceException += (_, eventArgs) =>
        {
            if (eventArgs.Exception.GetType() == typeof(System.Net.Sockets.SocketException) ||
                eventArgs.Exception.GetType() == typeof(System.IO.IOException) ||
                eventArgs.Exception.GetType() == typeof(System.Net.WebSockets.WebSocketException) ||
                eventArgs.Exception.GetType() == typeof(System.Threading.Tasks.TaskCanceledException) ||
                eventArgs.Exception.GetType() == typeof(Discord.WebSocket.GatewayReconnectException) ||
                eventArgs.Exception.GetType() == typeof(TwitchLib.Api.Core.Exceptions.InternalServerErrorException) ||
                eventArgs.Exception.GetType() == typeof(System.OperationCanceledException) ||
                eventArgs.Exception.GetType() == typeof(System.Threading.Tasks.TaskCanceledException) ||
                eventArgs.Exception.Message.Contains("JavaScript"))
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

    private static void RegisterDbServices(IServiceCollection services)
    {
        services.AddScoped(typeof(IGenericRepository<>), typeof(GenericRepository<>));
        services.AddScoped<IUnitOfWork, UnitOfWork>();
    }

    private static List<Type> RegisterCommandServices(IServiceCollection services)
    {
        var commands = new List<Type>();
        services.AddSingleton<DotNetTwitchBot.Bot.Commands.Features.IViewerFeature, DotNetTwitchBot.Bot.Commands.Features.ViewerFeature>();
        commands.Add(typeof(DotNetTwitchBot.Bot.Commands.Features.IViewerFeature));
        services.AddSingleton<DotNetTwitchBot.Bot.Commands.Features.ITicketsFeature, DotNetTwitchBot.Bot.Commands.Features.TicketsFeature>();
        commands.Add(typeof(DotNetTwitchBot.Bot.Commands.Features.ITicketsFeature));

        return commands;
    }
}