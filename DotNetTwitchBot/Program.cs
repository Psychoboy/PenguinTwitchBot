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
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Components.Server.Circuits;
using Microsoft.AspNetCore.HttpLogging;
using Microsoft.AspNetCore.HttpOverrides;
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
        builder.Services.AddSingleton<IDiscordService, DiscordService>();

        builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie();

        builder.Services.AddRazorPages();

        builder.Services.AddHttpLogging(options =>
        {
            options.LoggingFields = HttpLoggingFields.RequestPropertiesAndHeaders;
        });

        builder.Services.AddServerSideBlazor().AddHubOptions(hub => hub.MaximumReceiveMessageSize = 100 * 1024 * 1024); // 100 MB
        builder.Services.AddMudServices();

        //Database
        builder.Services.AddSingleton<IDatabaseTools, DatabaseTools>();
        builder.Services.AddTwitchLibEventSubWebsockets();
        builder.Services.AddBotCommands();




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
        });
        builder.Services.AddQuartzServer(
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
        app.Services.GetRequiredService<IDiscordService>();


        await app.Services.GetRequiredService<IDatabaseTools>().Backup();

        app.UseMiddleware<DotNetTwitchBot.CustomMiddleware.ErrorHandlerMiddleware>();

        // Configure the HTTP request pipeline.
        if (!app.Environment.IsDevelopment())
        {
            app.UseExceptionHandler("/Error");
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
        DotNetRuntimeStatsBuilder
            .Customize()
            .WithContentionStats(CaptureLevel.Informational)
            .WithJitStats()
            .WithThreadPoolStats()
            .WithGcStats()
            .WithExceptionStats(CaptureLevel.Errors)
            .StartCollecting();

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
            if (
                eventArgs.Exception.GetType() == typeof(System.Net.Sockets.SocketException) ||
                eventArgs.Exception.GetType() == typeof(System.Threading.Tasks.TaskCanceledException) ||
                eventArgs.Exception.GetType() == typeof(TwitchLib.Api.Core.Exceptions.InternalServerErrorException) ||
                eventArgs.Exception.GetType() == typeof(System.OperationCanceledException) ||
                eventArgs.Exception.GetType() == typeof(System.Threading.Tasks.TaskCanceledException) ||
                eventArgs.Exception.GetType() == typeof(Microsoft.AspNetCore.Connections.ConnectionAbortedException) ||
                eventArgs.Exception.GetType() == typeof(Discord.WebSocket.GatewayReconnectException) ||
                eventArgs.Exception.GetType() == typeof(System.IO.InvalidDataException) ||
                eventArgs.Exception.GetType() == typeof(System.Threading.Channels.ChannelClosedException) ||
                eventArgs.Exception.GetType() == typeof(System.Net.WebSockets.WebSocketException) ||
                eventArgs.Exception.Message.Contains("JavaScript")
                )
            {
                return; //Ignore
            }

            if (eventArgs.Exception.InnerException?.GetType() == typeof(System.Net.Sockets.SocketException))
            {
                var ex = eventArgs.Exception.InnerException as System.Net.Sockets.SocketException;
                if (ex?.SocketErrorCode == System.Net.Sockets.SocketError.OperationAborted) return;
                if (ex?.SocketErrorCode == System.Net.Sockets.SocketError.ConnectionAborted) return;
                if (ex?.SocketErrorCode == System.Net.Sockets.SocketError.ConnectionReset) return;
            }
            //special for Discord.NET ignore websocket exceptions

            logger.LogDebug(eventArgs.Exception, "Global Exception Caught from {sender} in {source}, InnerEx Begin\n{InnerException}\nInnerEx End", sender, eventArgs.Exception.Source, eventArgs.Exception.InnerException);
        };
        app.MapHub<DotNetTwitchBot.Bot.Commands.Music.YtHub>("/ythub");
        app.MapHub<DotNetTwitchBot.Bot.Hubs.MainHub>("/mainhub");

        app.MapBlazorHub();
        app.MapFallbackToPage("/_Host");
        await app.RunAsync(); //Start in future to read input

    }
}