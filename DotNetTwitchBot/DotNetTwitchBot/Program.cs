using DotNetTwitchBot.Bot;
using DotNetTwitchBot.Bot.Commands;
using DotNetTwitchBot.Bot.Events;
using TwitchLib.EventSub.Websockets.Extensions;

internal class Program
{
    private static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);
        builder.Configuration.AddJsonFile("appsettings.secrets.json");
        // Add services to the container.
        builder.Services.AddControllersWithViews();
        builder.Services.AddSingleton<CommandService>();
        builder.Services.AddSingleton<TwitchService>();
        builder.Services.AddHostedService<TwitchChatBot>();
        builder.Services.AddTwitchLibEventSubWebsockets();
        builder.Services.AddHostedService<TwitchWebsocketHostedService>();

        //Add Features Here:
        builder.Services.AddHostedService<DotNetTwitchBot.Bot.Commands.Features.TestFeature>();


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