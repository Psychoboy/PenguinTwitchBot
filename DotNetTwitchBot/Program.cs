global using System.ComponentModel.DataAnnotations;
global using System.ComponentModel.DataAnnotations.Schema;
global using Microsoft.EntityFrameworkCore;
global using DotNetTwitchBot.Bot.Models;
global using DotNetTwitchBot.Bot.Core.Database;
using System;
using System.Runtime.InteropServices;
using System.Drawing;
using System.Security.Cryptography.X509Certificates;
using Quartz;
using DotNetTwitchBot.Bot;
using DotNetTwitchBot.Bot.Core;
using Serilog;
using TwitchLib.EventSub.Websockets.Extensions;
using Serilog.Filters;

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
        builder.Services.AddSingleton<ServiceBackbone>();
        builder.Services.AddSingleton<TwitchService>();

        //Database
        builder.Services.AddSingleton<IDatabaseTools, DatabaseTools>();

        builder.Services.AddHostedService<TwitchChatBot>();
        builder.Services.AddTwitchLibEventSubWebsockets();
        builder.Services.AddHostedService<TwitchWebsocketHostedService>();
        builder.Services.AddSingleton<DotNetTwitchBot.Bot.Alerts.SendAlerts>();
        builder.Services.AddSingleton<DotNetTwitchBot.Bot.Notifications.IWebSocketMessenger, DotNetTwitchBot.Bot.Notifications.WebSocketMessenger>();

        //Add Features Here:
        var commands = new List<Type>();
        commands.Add(typeof(DotNetTwitchBot.Bot.Commands.Features.ViewerFeature));
        commands.Add(typeof(DotNetTwitchBot.Bot.Commands.Features.TicketsFeature));
        commands.Add(typeof(DotNetTwitchBot.Bot.Commands.Features.GiveawayFeature));
        commands.Add(typeof(DotNetTwitchBot.Bot.Commands.Games.WaffleRaffle));
        commands.Add(typeof(DotNetTwitchBot.Bot.Commands.Games.PancakeRaffle));
        commands.Add(typeof(DotNetTwitchBot.Bot.Commands.Games.BaconRaffle));
        commands.Add(typeof(DotNetTwitchBot.Bot.Commands.Misc.AddActive));
        commands.Add(typeof(DotNetTwitchBot.Bot.Commands.Misc.First));
        commands.Add(typeof(DotNetTwitchBot.Bot.Commands.Misc.DeathCounter));
        commands.Add(typeof(DotNetTwitchBot.Bot.Commands.Games.Roulette));
        commands.Add(typeof(DotNetTwitchBot.Bot.Commands.Custom.CustomCommand));
        commands.Add(typeof(DotNetTwitchBot.Bot.Commands.Custom.AudioCommands));
        commands.Add(typeof(DotNetTwitchBot.Bot.Commands.Features.LoyaltyFeature));



        //Add Alerts
        commands.Add(typeof(DotNetTwitchBot.Bot.Alerts.AlertImage));

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
            options.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString));
        });

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
        var viewerFeature = app.Services.GetRequiredService<DotNetTwitchBot.Bot.Commands.Features.ViewerFeature>();
        await viewerFeature.UpdateSubscribers();
        var customCommands = app.Services.GetRequiredService<DotNetTwitchBot.Bot.Commands.Custom.CustomCommand>();
        await customCommands.LoadCommands();
        var audioCommands = app.Services.GetRequiredService<DotNetTwitchBot.Bot.Commands.Custom.AudioCommands>();
        await audioCommands.LoadAudioCommands();
        foreach (var cmd in commands)
        {
            app.Services.GetRequiredService(cmd);
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

        app.Run(); //Start in future to read input
    }
}