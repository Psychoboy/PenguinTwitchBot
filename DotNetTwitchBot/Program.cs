using System.Security.Cryptography.X509Certificates;
using DotNetTwitchBot.Bot;
using DotNetTwitchBot.Bot.Core;
using DotNetTwitchBot.Bot.Core.Database;
using Serilog;
using TwitchLib.EventSub.Websockets.Extensions;

internal class Program
{
    private static void Main(string[] args)
    {
        
        var builder = WebApplication.CreateBuilder(args);
        builder.Configuration.AddJsonFile("appsettings.secrets.json");
        var path = builder.Configuration.GetValue<string>("Logging:FilePath");
        builder.Host.UseSerilog((ctx, lc) => lc
            .WriteTo.Console()
            .WriteTo.File(path, rollingInterval: RollingInterval.Day)
        );
        
        // Add services to the container.
        builder.Services.AddControllersWithViews();
        builder.Services.AddSingleton<EventService>();
        builder.Services.AddSingleton<TwitchService>();
        
        //Database
        builder.Services.Configure<LiteDbOptions>(builder.Configuration.GetSection("LiteDbOptions"));
        builder.Services.AddSingleton<ILiteDbContext, LiteDbContext>();
        builder.Services.AddSingleton<IDbViewerPoints, DbViewerPoints>();
        builder.Services.AddSingleton<IViewerData, ViewerData>();
        builder.Services.AddSingleton<IGiveawayEntries, GiveawayEntries>();

        builder.Services.AddHostedService<TwitchChatBot>();
        builder.Services.AddTwitchLibEventSubWebsockets();
        builder.Services.AddHostedService<TwitchWebsocketHostedService>();

        //Add Features Here:
        // builder.Services.AddHostedService<DotNetTwitchBot.Bot.Commands.Features.TestFeature>();
        builder.Services.AddSingleton<DotNetTwitchBot.Bot.Commands.Features.UserFeature>();
        builder.Services.AddHostedService<DotNetTwitchBot.Bot.Commands.Features.TicketFeature>();
        builder.Services.AddHostedService<DotNetTwitchBot.Bot.Commands.Features.GiveawayFeature>();
        
        
        // Log.Logger = new LoggerConfiguration()
        // // .MinimumLevel.Override("Microsoft", Serilog.Events.LogEventLevel.Information)
        // .Enrich.FromLogContext()
        // .WriteTo.File(path)
        // .CreateLogger();
        // LoggerFactory.Create(config => {
        //     new LoggerConfiguration()
        //     //.MinimumLevel.Override("Microsoft", Serilog.Events.LogEventLevel.Information)
        //     .Enrich.FromLogContext()
        //     .WriteTo.File(path, rollingInterval: RollingInterval.Day)
        //     .CreateLogger();
        // });

        var app = builder.Build();

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





        app.Run();
    }
}