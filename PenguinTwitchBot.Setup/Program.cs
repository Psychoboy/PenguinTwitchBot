using PenguinTwitchBot.Setup.Services;
using MudBlazor;
using MudBlazor.Services;
using System.Diagnostics;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorPages();
builder.Services.AddControllers();
builder.Services.AddServerSideBlazor();
builder.Services.AddMudServices(config =>
{
    config.SnackbarConfiguration.PositionClass = Defaults.Classes.Position.TopCenter;
    config.SnackbarConfiguration.PreventDuplicates = true;
    config.SnackbarConfiguration.VisibleStateDuration = 5000;
});

// Output path: first CLI arg, or --SecretsOutputPath config key, or default
var outputPath = args.FirstOrDefault(a => !a.StartsWith('-'))
    ?? builder.Configuration["SecretsOutputPath"]
    ?? "appsettings.secrets.json";

builder.Services.AddSingleton<SetupService>(sp => new SetupService(
    outputPath,
    sp.GetRequiredService<IHostApplicationLifetime>(),
    sp.GetRequiredService<ILogger<SetupService>>()));

var app = builder.Build();

app.UseStaticFiles();
app.UseRouting();
app.MapControllers();
app.MapBlazorHub();
app.MapFallbackToPage("/_Host");

const string listenUrl = "http://localhost:5000";
app.Lifetime.ApplicationStarted.Register(() =>
{
    Console.ForegroundColor = ConsoleColor.Cyan;
    Console.WriteLine();
    Console.WriteLine($"  Setup wizard running at: {listenUrl}");
    Console.WriteLine($"  Writing config to: {Path.GetFullPath(outputPath)}");
    Console.ResetColor();
    try { Process.Start(new ProcessStartInfo(listenUrl) { UseShellExecute = true }); }
    catch { /* best-effort browser open */ }
});

await app.RunAsync(listenUrl);
