using System.Runtime.InteropServices;
using System.Drawing;
using System.Security.Cryptography.X509Certificates;
using Quartz;
using DotNetTwitchBot.Bot;
using DotNetTwitchBot.Bot.Core;
using DotNetTwitchBot.Bot.Core.Database;
using Serilog;
using TwitchLib.EventSub.Websockets.Extensions;

internal class Program
{
    private static async Task Main(string[] args)
    {
        
        var builder = WebApplication.CreateBuilder(args);
        var section = builder.Configuration.GetSection("Secrets");
        var secretsFileLocation = section.GetValue<string>("SecretsConf");
        if(secretsFileLocation == null) throw new Exception("Invalid file configuration");
        builder.Configuration.AddJsonFile(secretsFileLocation);
        var path = builder.Configuration.GetValue<string>("Logging:FilePath");
        if(path == null) path = "";
        builder.Host.UseSerilog((ctx, lc) => lc
            .WriteTo.Console()
            .WriteTo.File(path, rollingInterval: RollingInterval.Day)
        );
        
        // Add services to the container.
        builder.Services.AddControllersWithViews();
        builder.Services.AddSingleton<EventService>();
        builder.Services.AddSingleton<TwitchService>();
        
        //Database
        builder.Services.AddSingleton<IDatabase, Database>();
        builder.Services.AddSingleton<GiveawayData>();
        builder.Services.AddSingleton<PointsData>();
        builder.Services.AddSingleton<ViewerData>();
        builder.Services.AddSingleton<FollowData>();

        builder.Services.AddHostedService<TwitchChatBot>();
        builder.Services.AddTwitchLibEventSubWebsockets();
        builder.Services.AddHostedService<TwitchWebsocketHostedService>();

        //Add Features Here:
        // builder.Services.AddHostedService<DotNetTwitchBot.Bot.Commands.Features.TestFeature>();
        builder.Services.AddSingleton<DotNetTwitchBot.Bot.Commands.Features.ViewerFeature>();
        builder.Services.AddSingleton<DotNetTwitchBot.Bot.Commands.Features.PointsFeature>();
        builder.Services.AddSingleton<DotNetTwitchBot.Bot.Commands.Features.GiveawayFeature>();
        

        //Backup Jobs:
        builder.Services.AddSingleton<DotNetTwitchBot.Bot.ScheduledJobs.BackupDbJob>();
        builder.Services.AddQuartz(q => {
            q.UseMicrosoftDependencyInjectionJobFactory();

            var backupDbJobKey = new JobKey("BackupDbJob");
            q.AddJob<DotNetTwitchBot.Bot.ScheduledJobs.BackupDbJob>(opts => opts.WithIdentity(backupDbJobKey));
            q.AddTrigger(opts => opts
                .ForJob(backupDbJobKey)
                .WithIdentity("BackupDb-Trigger")
                .WithCronSchedule("0 0 12 * * ?") //Every day at noon
            );
            q.AddTrigger(opts => opts
                .ForJob(backupDbJobKey)
                .WithIdentity("BackupDb-Trigger")
                .StartAt(new DateTimeOffset(DateTime.Now.AddMinutes(1)))
            );
        });
        builder.Services.AddQuartzHostedService(
            q => q.WaitForJobsToComplete = true
        );
        // var section = builder.Configuration.GetSection("Database");
        // var test = section.GetValue<string>("DbLocation");
        var app = builder.Build();

        //Force load/startup
        var viewerFeature = app.Services.GetRequiredService<DotNetTwitchBot.Bot.Commands.Features.ViewerFeature>();
        await viewerFeature.UpdateSubscribers();
        var pointsFeature = app.Services.GetRequiredService<DotNetTwitchBot.Bot.Commands.Features.PointsFeature>();
        var giveawayFeature = app.Services.GetRequiredService<DotNetTwitchBot.Bot.Commands.Features.GiveawayFeature>();
        

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

        app.MapControllerRoute(
            name: "default",
            pattern: "{controller=Home}/{action=Index}/{id?}");





        app.Run(); //Start in future to read input
    }
}