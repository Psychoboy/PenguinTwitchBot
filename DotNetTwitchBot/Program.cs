global using DotNetTwitchBot.Bot.Core.Database;
global using DotNetTwitchBot.Bot.Models;
global using Microsoft.EntityFrameworkCore;
global using System.ComponentModel.DataAnnotations;
global using System.ComponentModel.DataAnnotations.Schema;
using DotNetTwitchBot.Bot.Core;
using DotNetTwitchBot.Bot.TwitchServices;
using DotNetTwitchBot.Circuit;
using DotNetTwitchBot.CustomMiddleware;
using DotNetTwitchBot.HealthChecks;
using DotNetTwitchBot.Repository;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Components.Server.Circuits;
using Microsoft.AspNetCore.HttpLogging;
using Microsoft.AspNetCore.HttpOverrides;
using MudBlazor;
using MudBlazor.Services;
using Prometheus;
using Prometheus.DotNetRuntime;
using Quartz;
using Quartz.AspNetCore;
using Serilog;
using System.Net;

using TwitchLib.EventSub.Websockets.Extensions;
internal class Program
{
    private static ILogger<Program>? logger;
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
        //builder.Services.AddSingleton<IDiscordService, DiscordService>();

        builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie();

        builder.Services.AddRazorPages();

        builder.Services.AddHttpLogging(options =>
        {
            options.LoggingFields = HttpLoggingFields.RequestPropertiesAndHeaders;
        });

        builder.Services.AddServerSideBlazor().AddHubOptions(hub => hub.MaximumReceiveMessageSize = 100 * 1024 * 1024); // 100 MB
        builder.Services.AddMudServices(config =>
        {
            config.SnackbarConfiguration.PositionClass = Defaults.Classes.Position.TopCenter;
            config.SnackbarConfiguration.PreventDuplicates = true;
            config.SnackbarConfiguration.NewestOnTop = false;
            config.SnackbarConfiguration.ShowCloseIcon = true;
            config.SnackbarConfiguration.VisibleStateDuration = 5000;
            config.SnackbarConfiguration.HideTransitionDuration = 500;
            config.SnackbarConfiguration.ShowTransitionDuration = 500;
            config.SnackbarConfiguration.SnackbarVariant = Variant.Filled;
        });

        //Database
        builder.Services.AddSingleton<IDatabaseTools, DatabaseTools>();
        builder.Services.AddTwitchLibEventSubWebsockets();
        builder.Services.AddBotCommands();




        builder.Services.AddQuartz(q =>
        {
            var backupDbJobKey = new JobKey("BackupDbJob");
            var updateDiscordEventsKey = new JobKey("UpdateDiscordEvents");
            var UpdatePostedScheduleKey = new JobKey("UpdatePostedSchedule");
            var cleanupChatLogs = new JobKey("CleanUpChatLogs");
            var hourlyCLeanup = new JobKey("HourlyCleanup");
            q.AddJob<DotNetTwitchBot.Bot.ScheduledJobs.BackupDbJob>(opts => opts.WithIdentity(backupDbJobKey));
            q.AddTrigger(opts => opts
                .ForJob(backupDbJobKey)
                .WithIdentity("BackupDb-Trigger")
                .WithCronSchedule(CronScheduleBuilder.DailyAtHourAndMinute(12, 00)) //Every day at noon
            );

            q.AddJob<DotNetTwitchBot.Bot.ScheduledJobs.UpdateDiscordEvents>(opts => opts.WithIdentity(updateDiscordEventsKey));
            q.AddTrigger(opts => opts
                .ForJob(updateDiscordEventsKey)
                .WithIdentity("UpdateDiscord-Trigger")
                .WithCronSchedule(CronScheduleBuilder.CronSchedule("0 0/5 * * * ?"))
            );

            q.AddJob<DotNetTwitchBot.Bot.ScheduledJobs.UpdatePostedSchedule>(opts => opts.WithIdentity(UpdatePostedScheduleKey));
            q.AddTrigger(opts => opts
                .ForJob(UpdatePostedScheduleKey)
                .WithIdentity("UpdateDiscordSchedule-Trigger")
                .WithCronSchedule(CronScheduleBuilder.WeeklyOnDayAndHourAndMinute(DayOfWeek.Monday, 9, 0))
                );

            q.AddJob<DotNetTwitchBot.Bot.ScheduledJobs.CleanupChatLogJob>(opts => opts.WithIdentity(cleanupChatLogs));
            q.AddTrigger(opts => opts
                .ForJob(cleanupChatLogs)
                .WithIdentity("CleanUpChatLogs-Trigger")
                .WithCronSchedule(CronScheduleBuilder.DailyAtHourAndMinute(13, 00)) //Every day at 1PM
            );
            q.AddJob<DotNetTwitchBot.Bot.ScheduledJobs.HourlyCleanupJob>(opts => opts.WithIdentity(hourlyCLeanup));
            q.AddTrigger(opts => opts
                .ForJob(hourlyCLeanup)
                .WithIdentity("HourlyCleanup-Trigger")
                .WithSimpleSchedule(x => x.WithIntervalInHours(1).RepeatForever().Build()) //Every Hour
            );
        });
        builder.Services.AddQuartzServer(
            q => q.WaitForJobsToComplete = true
        );

        //builder.Services.AddAntiforgery(o => o.SuppressXFrameOptionsHeader = true);

        var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
        builder.Services.AddDbContext<ApplicationDbContext>(options =>
            {
                options.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString), options => options.EnableRetryOnFailure(
                    maxRetryCount: 5,
                    maxRetryDelay: System.TimeSpan.FromSeconds(15),
                    errorNumbersToAdd: null
                ).TranslateParameterizedCollectionsToConstants());
            });

        builder.Services.AddSignalR();

        builder.Services.AddSingleton<ICircuitUserService, CircuitUserService>();
        builder.Services.AddScoped<CircuitHandler>((sp) =>
            new CircuitHandlerService(sp.GetRequiredService<ICircuitUserService>()));


        builder.Configuration.GetRequiredSection("Discord").Get<DiscordSettings>();

        builder.Services.AddHealthChecks()
            .AddCheck<TwitchBotHealthCheck>("TwitchChatBot")
            .AddCheck<CommandServiceHealthCheck>("ServiceBackbone")
            .AddCheck<DiscordServiceHealthCheck>("DiscordBot")
            .ForwardToPrometheus();
        builder.Services.AddScoped<BlazorAppContext>();
        builder.Services.AddHttpContextAccessor();

        builder.Services.Configure<ForwardedHeadersOptions>(options =>
        {
            options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
            options.KnownProxies.Add(IPAddress.Parse("192.168.1.128"));
            options.RequireHeaderSymmetry = false;
            options.ForwardLimit = null;
        });

        var app = builder.Build();
        app.UseAuthentication();
        app.UseAuthorization();
        app.UseForwardedHeaders();

        if (!app.Environment.IsDevelopment())
        {
            using var scope = app.Services.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            dbContext.Database.Migrate();
        }

        //Loads all the command stuff into memory
        //app.Services.GetRequiredService<IDiscordService>();


        await app.Services.GetRequiredService<IDatabaseTools>().Backup();

        app.UseMiddleware<DotNetTwitchBot.CustomMiddleware.ErrorHandlerMiddleware>();

        // Configure the HTTP request pipeline.
        if (!app.Environment.IsDevelopment())
        {
            app.UseExceptionHandler("/Error");
            // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
            //app.UseHsts();

        }

        //app.UseHttpsRedirection();
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
        DotNetRuntimeStatsBuilder
            .Customize()
            .WithContentionStats(CaptureLevel.Informational)
            .WithJitStats()
            .WithThreadPoolStats()
            .WithGcStats()
            .WithExceptionStats(CaptureLevel.Errors)
            .StartCollecting();

        logger = app.Services.GetRequiredService<ILogger<Program>>();
        var lifetime = app.Lifetime;
        lifetime.ApplicationStarted.Register(() =>
        {
            logger?.LogInformation("Application Starting");
        });
        AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;

        var websocketMessenger = app.Services.GetRequiredService<DotNetTwitchBot.Bot.Notifications.IWebSocketMessenger>();
        lifetime.ApplicationStopping.Register(async () =>
        {
            logger?.LogInformation("Application trying to stop.");
            await websocketMessenger.CloseAllSockets();
        });

        app.MapHub<DotNetTwitchBot.Bot.Commands.Music.YtHub>("/ythub");
        app.MapHub<DotNetTwitchBot.Bot.Hubs.MainHub>("/mainhub");
        app.MapBlazorHub();
        app.MapFallbackToPage("/_Host");
        try
        {
            if (!File.Exists("yt-dlp.exe"))
            {
                await YoutubeDLSharp.Utils.DownloadYtDlp();
                await YoutubeDLSharp.Utils.DownloadFFmpeg();
            }
        }
        catch (Exception _) { }
        await app.RunAsync(); //Start in future to read input

    }

    private static void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is Exception ex)
        {
            Log.Fatal("Unhandled exception:", ex);
        }
        else
        {
            Log.Fatal("Unhandled non-exception object:", e.ExceptionObject);
        }
    }
}