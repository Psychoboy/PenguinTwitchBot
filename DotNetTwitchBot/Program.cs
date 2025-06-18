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
using LinqToDB.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Components.Server.Circuits;
using Microsoft.AspNetCore.HttpLogging;
using Microsoft.AspNetCore.HttpOverrides;
using MudBlazor;
using MudBlazor.Services;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Prometheus;
using Prometheus.DotNetRuntime;
using Quartz;
using Quartz.AspNetCore;
using Serilog;
using Serilog.Enrichers.Span;
using Serilog.Sinks.OpenTelemetry;
using System.Collections;
using System.Diagnostics;
using System.Net;
using System.Net.NetworkInformation;
using TwitchLib.EventSub.Websockets.Extensions;
internal class Program
{
    private static ILogger<Program>? logger;
    private static async Task Main(string[] args)
    {
        Environment.SetEnvironmentVariable("OTEL_DOTNET_AUTO_INSTRUMENTATION_ENABLED", "true");
        Environment.SetEnvironmentVariable("OTEL_SERVICE_NAME", "DotNetTwitchBot");
        using var server = new Prometheus.KestrelMetricServer(port: 4999);
        server.Start();
        var builder = WebApplication.CreateBuilder(args);
        var section = builder.Configuration.GetSection("Secrets");
        var secretsFileLocation = section.GetValue<string>("SecretsConf") ?? throw new Exception("Invalid file configuration");
        builder.Configuration.AddJsonFile(secretsFileLocation);
        Activity.DefaultIdFormat = System.Diagnostics.ActivityIdFormat.W3C;
        var loggerConfiguration = new LoggerConfiguration()
           .ReadFrom.Configuration(builder.Configuration)
           .Enrich.WithSpan()
           .Enrich.FromLogContext()
           .WriteTo.OpenTelemetry(options =>
           {
               options.Endpoint = "http://localhost:4318"; // Adjust the endpoint as needed
               options.Protocol = Serilog.Sinks.OpenTelemetry.OtlpProtocol.HttpProtobuf; // Use the appropriate protocol
               options.IncludedData = IncludedData.MessageTemplateTextAttribute |
                                      IncludedData.TraceIdField | IncludedData.SpanIdField |
                                      IncludedData.SpecRequiredResourceAttributes; 
               options.ResourceAttributes = new Dictionary<string, object>
               {
                   { "service.name", "DotNetTwitchBot" },
                   { "service.environment", builder.Environment.EnvironmentName }
               };
           });

        builder.Logging.ClearProviders();

        builder.Logging.AddSerilog(loggerConfiguration.CreateLogger());

        // Add OpenTelemetry data to json logs
        builder.Services.AddOpenTelemetry()
            .ConfigureResource(resource => resource.AddService("DotNetTwitchBot"))
            .WithTracing(tracing => tracing
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation()
                .AddOtlpExporter(otlp =>
                {
                    otlp.Protocol = OpenTelemetry.Exporter.OtlpExportProtocol.HttpProtobuf;
                }))
            .WithMetrics(metrics => metrics
                .AddAspNetCoreInstrumentation());

        // Add services to the container.
        builder.Services.AddControllersWithViews();
        builder.Services.AddSingleton<SettingsFileManager>();
        builder.Services.AddSingleton<ILanguage, Language>();

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
            var postScheduleKey = new JobKey("PostSchedule");
            var cleanupChatLogs = new JobKey("CleanUpChatLogs");
            var hourlyCLeanup = new JobKey("HourlyCleanup");
            var updatePostedSchedulekey = new JobKey("UpdatePostedSchedule");
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

            q.AddJob<DotNetTwitchBot.Bot.ScheduledJobs.PostSchedule>(opts => opts.WithIdentity(postScheduleKey));
            q.AddTrigger(opts => opts
                .ForJob(postScheduleKey)
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
            q.AddJob<DotNetTwitchBot.Bot.ScheduledJobs.UpdatePostedSchedule>(opts => opts.WithIdentity(updatePostedSchedulekey));
            q.AddTrigger(opts => opts
                .ForJob(updatePostedSchedulekey)
                .WithIdentity("UpdatePostedSchedule-Trigger")
                .WithCronSchedule(CronScheduleBuilder.DailyAtHourAndMinute(10, 00)) //Every day at 10AM
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
                )
                .TranslateParameterizedCollectionsToConstants()
                .EnablePrimitiveCollectionsSupport());
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
            options.RequireHeaderSymmetry = false;
            options.ForwardLimit = null;
            foreach(var network in GetNetworks(NetworkInterfaceType.Ethernet))
            {
                options.KnownNetworks.Add(network);
            }
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
        catch (Exception) { }
        LinqToDBForEFTools.Initialize();
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

    private static IEnumerable<Microsoft.AspNetCore.HttpOverrides.IPNetwork> GetNetworks(NetworkInterfaceType type)
    {

        foreach (var item in NetworkInterface.GetAllNetworkInterfaces()
            .Where(n => n.NetworkInterfaceType == type && n.OperationalStatus == OperationalStatus.Up)  // get all operational networks of a given type
            .Select(n => n.GetIPProperties())   // get the IPs
            .Where(n => n.GatewayAddresses.Any())) // where the IPs have a gateway defined
        {
            var ipInfo = item.UnicastAddresses.FirstOrDefault(i => i.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork); // get the first cluster-facing IP address
            if (ipInfo == null) { continue; }

            // convert the mask to bits
            var maskBytes = ipInfo.IPv4Mask.GetAddressBytes();
            if (!BitConverter.IsLittleEndian)
            {
                Array.Reverse(maskBytes);
            }
            var maskBits = new BitArray(maskBytes);

            // count the number of "true" bits to get the CIDR mask
            var cidrMask = maskBits.Cast<bool>().Count(b => b);

            // convert my application's ip address to bits
            var ipBytes = ipInfo.Address.GetAddressBytes();
            if (!BitConverter.IsLittleEndian)
            {
                Array.Reverse(ipBytes);
            }
            var ipBits = new BitArray(ipBytes);

            // and the bits with the mask to get the start of the range
            var maskedBits = ipBits.And(maskBits);

            // Convert the masked IP back into an IP address
            var maskedIpBytes = new byte[4];
            maskedBits.CopyTo(maskedIpBytes, 0);
            if (!BitConverter.IsLittleEndian)
            {
                Array.Reverse(maskedIpBytes);
            }
            var rangeStartIp = new IPAddress(maskedIpBytes);

            // return the start IP and CIDR mask
            yield return new(rangeStartIp, cidrMask);
        }
    }
}