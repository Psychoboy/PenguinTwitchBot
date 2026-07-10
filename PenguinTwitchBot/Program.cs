global using PenguinTwitchBot.Bot.Core.Database;
global using PenguinTwitchBot.Database.Bot.Core.Database;
global using PenguinTwitchBot.Database.Bot.Models;
global using PenguinTwitchBot.Bot.Models;
global using PenguinTwitchBot.Database.Bot.Core;
global using PenguinTwitchBot.Database.Bot.Actions;
global using PenguinTwitchBot.Database.Bot.DatabaseTools;
global using PenguinTwitchBot.Database.Repository;
global using PenguinTwitchBot.Database.Repository.Repositories;
global using PenguinTwitchBot.Services;
global using Microsoft.EntityFrameworkCore;
global using System.ComponentModel.DataAnnotations;
global using System.ComponentModel.DataAnnotations.Schema;
using PenguinTwitchBot.Application.Notifications;
using PenguinTwitchBot.Bot.Core;
using PenguinTwitchBot.Bot.TwitchServices;
using PenguinTwitchBot.Circuit;
using PenguinTwitchBot.CustomMiddleware;
using PenguinTwitchBot.HealthChecks;
using Google.Api;
using LinqToDB.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Components.Server.Circuits;
using Microsoft.AspNetCore.HttpLogging;
using Microsoft.AspNetCore.HttpOverrides;
using MudBlazor;
using MudBlazor.Services;
using OpenAI;
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
using Serilog.Events;
using Serilog.Sinks.OpenTelemetry;
using System.IO.Abstractions;
using System.Collections;
using System.Diagnostics;
using System.Net;
using System.Net.NetworkInformation;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Microsoft.AspNetCore.Server.Kestrel.Core;
internal class Program
{
    private static ILogger<Program>? logger;
    private static int _fatalSignalCount;
    private static X509Certificate2? _generatedHttpsCertificate;

    private static async Task Main(string[] args)
    {
        // Pre-load migration assemblies from the application directory so Assembly.Load(name)
        // can find them in self-contained / single-file publish scenarios.
        PreloadMigrationAssemblies();

        var builder = WebApplication.CreateBuilder(args);
        var section = builder.Configuration.GetSection("Secrets");
        var secretsFileLocation = section.GetValue<string>("SecretsConf") ?? "appsettings.secrets.json";
        if (!File.Exists(secretsFileLocation))
        {
            PrintSetupRequiredBanner(secretsFileLocation);
            if (!Console.IsInputRedirected)
                Console.ReadKey(intercept: true);
            Environment.ExitCode = 1;
            return;
        }
        builder.Configuration.AddJsonFile(secretsFileLocation, optional: false, reloadOnChange: true);

        var prometheusEnabled = builder.Configuration.GetValue<bool>("Observability:Prometheus:Enabled");
        var prometheusPort = builder.Configuration.GetValue<int>("Observability:Prometheus:Port", 4999);
        var otelEnabled = builder.Configuration.GetValue<bool>("Observability:OpenTelemetry:Enabled");
        var otelEndpoint = builder.Configuration.GetValue<string>("Observability:OpenTelemetry:Endpoint") ?? "http://localhost:4318";

        IDisposable? metricsServer = null;
        if (prometheusEnabled)
        {
            var ks = new Prometheus.KestrelMetricServer(port: prometheusPort);
            ks.Start();
            metricsServer = ks;
        }
        if (otelEnabled)
        {
            Environment.SetEnvironmentVariable("OTEL_DOTNET_AUTO_INSTRUMENTATION_ENABLED", "true");
            Environment.SetEnvironmentVariable("OTEL_SERVICE_NAME", "PenguinTwitchBot");
        }
        Activity.DefaultIdFormat = System.Diagnostics.ActivityIdFormat.W3C;
        var readerOptions = new Serilog.Settings.Configuration.ConfigurationReaderOptions(
            typeof(ConsoleLoggerConfigurationExtensions).Assembly,
            typeof(Serilog.Expressions.SerilogExpression).Assembly,
            typeof(Serilog.RollingInterval).Assembly,
            typeof(Serilog.Sinks.OpenTelemetry.OtlpProtocol).Assembly,
            typeof(Serilog.Sinks.File.FileSink).Assembly,
            typeof(Serilog.Log).Assembly
        );
        var loggerConfiguration = new LoggerConfiguration()
           .ReadFrom.Configuration(builder.Configuration, readerOptions)
           .Enrich.WithSpan()
           .Enrich.FromLogContext()
           .Filter.ByExcluding(logEvent =>
           {
               return IsExpectedTransientBlazorException(logEvent.Exception, logEvent.Properties);
           });
        if (otelEnabled && !string.IsNullOrEmpty(otelEndpoint))
        {
            loggerConfiguration = loggerConfiguration.WriteTo.OpenTelemetry(options =>
            {
                options.Endpoint = otelEndpoint;
                options.Protocol = Serilog.Sinks.OpenTelemetry.OtlpProtocol.HttpProtobuf;
                options.IncludedData = IncludedData.MessageTemplateTextAttribute |
                                       IncludedData.TraceIdField | IncludedData.SpanIdField |
                                       IncludedData.SpecRequiredResourceAttributes;
                options.ResourceAttributes = new Dictionary<string, object>
                {
                    { "service.name", "PenguinTwitchBot" },
                    { "service.environment", builder.Environment.EnvironmentName }
                };
            });
        }

        var serilogLogger = loggerConfiguration.CreateLogger();
        Log.Logger = serilogLogger;

        builder.Logging.ClearProviders();
        builder.Logging.AddSerilog(serilogLogger, dispose: false);

        RegisterGlobalExceptionHandlers();

        if (otelEnabled && !string.IsNullOrEmpty(otelEndpoint))
        {
            // Add OpenTelemetry data to json logs
            builder.Services.AddOpenTelemetry()
                .ConfigureResource(resource => resource.AddService("PenguinTwitchBot"))
                .WithTracing(tracing => tracing
                    .AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation()
                    .AddOtlpExporter(otlp =>
                    {
                        otlp.Endpoint = new Uri(otelEndpoint);
                        otlp.Protocol = OpenTelemetry.Exporter.OtlpExportProtocol.HttpProtobuf;
                    }))
                .WithMetrics(metrics => metrics
                    .AddAspNetCoreInstrumentation());
        }

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

        builder.Services.AddServerSideBlazor(options =>
        {
            options.DetailedErrors = builder.Environment.IsDevelopment();
        })
        .AddHubOptions(hub => hub.MaximumReceiveMessageSize = 100 * 1024 * 1024); // 100 MB
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
        builder.Services.AddMudMarkdownServices();

        builder.Services.AddSingleton<IDatabaseTools, DatabaseTools>();
        builder.Services.AddSingleton<IFileSystem>(sp => new System.IO.Abstractions.FileSystem());
        builder.Services.AddSingleton<IBackupTools, BackupTools>();
        builder.Services.AddSingleton<IZipService, ZipService>();
        builder.Services.AddPenguinDispatcher(typeof(Program).Assembly);
        builder.Services.AddBotCommands();




        builder.Services.AddQuartz(q =>
        {
            var backupDbJobKey = new JobKey(IScheduledJobSettingsService.TriggerBackupJobName);
            var cleanupClipsJobKey = new JobKey(IScheduledJobSettingsService.CleanupClipsJobName);
            var updateDiscordEventsKey = new JobKey(IScheduledJobSettingsService.UpdateDiscordEventsJobName);
            var postScheduleKey = new JobKey(IScheduledJobSettingsService.PostScheduleJobName);
            var cleanupChatLogs = new JobKey(IScheduledJobSettingsService.CleanupChatLogsJobName);
            var cleanupIpLogs = new JobKey(IScheduledJobSettingsService.CleanupIpLogsJobName);
            var cleanupCooldowns = new JobKey(IScheduledJobSettingsService.CleanupCooldownsJobName);
            var ttsCleanup = new JobKey(IScheduledJobSettingsService.TtsCleanupJobName);
            var updatePostedSchedulekey = new JobKey(IScheduledJobSettingsService.UpdatePostedScheduleJobName);
            var validationSanityCheckKey = new JobKey(IScheduledJobSettingsService.ValidationSanityCheckJobName);

            q.AddJob<PenguinTwitchBot.Bot.ScheduledJobs.TriggerBackupJob>(opts => opts.WithIdentity(backupDbJobKey).StoreDurably());
            q.AddJob<PenguinTwitchBot.Bot.ScheduledJobs.CleanupClipsJob>(opts => opts.WithIdentity(cleanupClipsJobKey).StoreDurably());
            q.AddJob<PenguinTwitchBot.Bot.ScheduledJobs.UpdateDiscordEvents>(opts => opts.WithIdentity(updateDiscordEventsKey).StoreDurably());
            q.AddJob<PenguinTwitchBot.Bot.ScheduledJobs.PostSchedule>(opts => opts.WithIdentity(postScheduleKey).StoreDurably());
            q.AddJob<PenguinTwitchBot.Bot.ScheduledJobs.CleanupChatLogJob>(opts => opts.WithIdentity(cleanupChatLogs).StoreDurably());
            q.AddJob<PenguinTwitchBot.Bot.ScheduledJobs.CleanupIpLogsJob>(opts => opts.WithIdentity(cleanupIpLogs).StoreDurably());
            q.AddJob<PenguinTwitchBot.Bot.ScheduledJobs.CleanupCooldownsJob>(opts => opts.WithIdentity(cleanupCooldowns).StoreDurably());
            q.AddJob<PenguinTwitchBot.Bot.ScheduledJobs.TtsCleanupJob>(opts => opts.WithIdentity(ttsCleanup).StoreDurably());
            q.AddJob<PenguinTwitchBot.Bot.ScheduledJobs.UpdatePostedSchedule>(opts => opts.WithIdentity(updatePostedSchedulekey).StoreDurably());
            q.AddJob<PenguinTwitchBot.Bot.ScheduledJobs.ValidationSanityCheckJob>(opts => opts.WithIdentity(validationSanityCheckKey).StoreDurably());
        });
        builder.Services.AddQuartzServer(
            q => q.WaitForJobsToComplete = true
        );

        //builder.Services.AddAntiforgery(o => o.SuppressXFrameOptionsHeader = true);

        var databaseProvider = GetDatabaseProvider(builder.Configuration);
        var connectionString = GetConnectionString(builder.Configuration, databaseProvider);
        var migrationsAssembly = GetMigrationsAssembly(databaseProvider);
        Log.Information("Using database provider {Provider}", databaseProvider);

        builder.Services.AddDbContext<ApplicationDbContext>(options =>
        {
            ConfigureDbContextOptions(options, databaseProvider, connectionString, migrationsAssembly);
        });

        builder.Services.AddSignalR();

        // Always register circuit services — MainLayout injects these and Blazor's SSR
        // prerender instantiates it even when the page specifies its own layout.
        builder.Services.AddSingleton<PenguinTwitchBot.Circuit.IpLog>();
        builder.Services.AddSingleton<ICircuitUserService, CircuitUserService>();
        builder.Services.AddScoped<CircuitHandler>((sp) =>
            new CircuitHandlerService(
                sp.GetRequiredService<ICircuitUserService>(),
                sp.GetRequiredService<ILogger<CircuitHandlerService>>()));

        builder.Configuration.GetRequiredSection("Discord").Get<DiscordSettings>();

        var openAiConf = builder.Configuration.GetRequiredSection("OpenAI").Get<OpenAiSettings>();
            if (openAiConf != null && !string.IsNullOrEmpty(openAiConf.ApiKey))
            {
                builder.Services.AddSingleton<OpenAIClient>(serviceProvider =>
                {
                    return new OpenAIClient(openAiConf.ApiKey);
                });
                builder.Services.AddScoped<PenguinTwitchBot.Bot.Ai.IStarCitizenAI, PenguinTwitchBot.Bot.Ai.StarCitizenAI>();
                builder.Services.AddScoped<PenguinTwitchBot.Bot.Ai.IShoutoutAi, PenguinTwitchBot.Bot.Ai.ShoutoutAi>();
            }

            builder.Services.AddHealthChecks()
                .AddCheck<TwitchBotHealthCheck>("TwitchChatBot")
                .AddCheck<CommandServiceHealthCheck>("ServiceBackbone")
                .AddCheck<DiscordServiceHealthCheck>("DiscordBot")
                .ForwardToPrometheus();
        builder.Services.AddScoped<BlazorAppContext>();
        builder.Services.AddHttpContextAccessor();
        builder.Services.AddScoped<PenguinTwitchBot.Services.HomepageLayoutService>();
        builder.Services.AddScoped<PenguinTwitchBot.Services.LeaderboardsLayoutService>();
        builder.Services.AddScoped<IBackupSettingsService, BackupSettingsService>();
        builder.Services.AddSingleton<IChatHistoryRetentionSettingsService, ChatHistoryRetentionSettingsService>();
        builder.Services.AddSingleton<IIpLogRetentionSettingsService, IpLogRetentionSettingsService>();
        builder.Services.AddSingleton<IScheduledJobSettingsService, ScheduledJobSettingsService>();
        builder.Services.AddSingleton<ICooldownCleanupService, CooldownCleanupService>();
        builder.Services.AddSingleton<IFileCleanupService, FileCleanupService>();
        builder.Services.AddScoped<PenguinTwitchBot.Services.ImageProcessingService>();
        builder.Services.AddScoped<PenguinTwitchBot.Services.DiscordLookupService>();
        builder.Services.AddHttpClient("GitHubRelease", c =>
        {
            c.DefaultRequestHeaders.UserAgent.ParseAdd("PenguinTwitchBot");
        });
        builder.Services.AddHttpClient("Emotes", c =>
        {
            c.DefaultRequestHeaders.UserAgent.ParseAdd("PenguinTwitchBot/1.0");
        });
        builder.Services.AddSingleton<PenguinTwitchBot.Services.VersionCheckService>();
        builder.Services.AddSingleton<PenguinTwitchBot.Services.IVersionCheckService>(sp =>
            sp.GetRequiredService<PenguinTwitchBot.Services.VersionCheckService>());
        builder.Services.AddHostedService(sp =>
            sp.GetRequiredService<PenguinTwitchBot.Services.VersionCheckService>());

        builder.Services.AddSingleton<PenguinTwitchBot.Bot.Services.Chat.IChatColorService,
            PenguinTwitchBot.Bot.Services.Chat.ChatColorService>();

        builder.Services.Configure<ForwardedHeadersOptions>(options =>
        {
            options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto | ForwardedHeaders.XForwardedHost;
            options.RequireHeaderSymmetry = false;
            options.ForwardLimit = null;
            options.KnownIPNetworks.Add(System.Net.IPNetwork.Parse("10.0.0.0/8"));
            options.KnownIPNetworks.Add(System.Net.IPNetwork.Parse("172.16.0.0/12"));
            options.KnownIPNetworks.Add(System.Net.IPNetwork.Parse("192.168.0.0/16"));
        });

        builder.WebHost.ConfigureKestrel((context, options) => ConfigureKestrelHttps(context, options));

        var app = builder.Build();
        app.UseAuthentication();
        app.UseAuthorization();
        app.UseForwardedHeaders();

        // Always migrate — for SQLite this creates the database file and schema on first run.
        // For PostgreSQL the server must be reachable before starting the app.
        {
            using var scope = app.Services.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var dbPath = dbContext.Database.GetConnectionString();
            if (dbPath != null)
            {
                // Extract the file path from the connection string (e.g. "Data Source=Data/foo.sqlite")
                var match = System.Text.RegularExpressions.Regex.Match(dbPath, @"Data Source=([^;]+)", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                if (match.Success)
                {
                    var dir = Path.GetDirectoryName(match.Groups[1].Value);
                    if (!string.IsNullOrEmpty(dir))
                        Directory.CreateDirectory(dir);
                }
            }
            dbContext.Database.Migrate();
        }

        await app.Services.GetRequiredService<IDatabaseTools>().Backup();
        await ConfigureQuartzTriggersAsync(app.Services);

        app.UseMiddleware<PenguinTwitchBot.CustomMiddleware.ErrorHandlerMiddleware>();

        // Configure the HTTP request pipeline.
        if (!app.Environment.IsDevelopment())
        {
            app.UseExceptionHandler("/Error");
            // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.

        }

        app.UseStatusCodePagesWithReExecute("/NotFoundItem", "?statusCode={0}");

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

        if (prometheusEnabled)
        {
            app.UseHttpMetrics();
            DotNetRuntimeStatsBuilder
                .Customize()
                .WithContentionStats(CaptureLevel.Informational)
                .WithThreadPoolStats()
                .WithGcStats()
                .WithExceptionStats(CaptureLevel.Errors)
                .StartCollecting();
        }

        logger = app.Services.GetRequiredService<ILogger<Program>>();
        var lifetime = app.Lifetime;
        lifetime.ApplicationStarted.Register(() =>
        {
            _ = RunStartupInitializationAsync(app, logger);
        });
        AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;

        var websocketMessenger = app.Services.GetRequiredService<PenguinTwitchBot.Bot.Notifications.IWebSocketMessenger>();
        var wsEventHandler = app.Services.GetRequiredService<PenguinTwitchBot.Bot.WebSocketEvents.IWsEventHandler>();
        lifetime.ApplicationStopping.Register(() =>
            {
                logger?.LogInformation("Application trying to stop.");
                // Fire-and-forget: don't block the shutdown thread. A blocked synchronous wait
                // ties up a thread-pool thread and can itself cause starvation during shutdown.
                _ = Task.Run(async () =>
                {
                    try
                    {
                        var closeTask = Task.WhenAll(
                            websocketMessenger.CloseAllSockets(),
                            wsEventHandler.CloseAllSockets()
                        );
                        if (await Task.WhenAny(closeTask, Task.Delay(TimeSpan.FromSeconds(5))) != closeTask)
                        {
                            logger?.LogWarning("WebSocket close did not complete within 5 s during shutdown; proceeding anyway.");
                        }
                        else
                        {
                            await closeTask; // propagate any exceptions from the close tasks
                        }
                    }
                    catch (Exception ex)
                    {
                        logger?.LogError(ex, "Error while closing websocket resources during shutdown.");
                    }
                });
            });

        app.MapHub<PenguinTwitchBot.Bot.Commands.Music.YtHub>("/ythub");
        app.MapHub<PenguinTwitchBot.Bot.Hubs.MainHub>("/mainhub");
        app.MapBlazorHub();
        app.MapFallbackToPage("/_Host");

try
            {
                await YoutubeDLSharp.Utils.DownloadYtDlp();
                await YoutubeDLSharp.Utils.DownloadFFmpeg();
            }
            catch (Exception)
            {
                // Ignore download failures - yt-dlp/ffmpeg fallback handled elsewhere or not critical for startup
            }

        LinqToDBForEFTools.Initialize();

        try
        {
            await app.RunAsync(); //Start in future to read input
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "Host terminated unexpectedly");
            throw;
        }
        finally
        {
            metricsServer?.Dispose();
            Log.CloseAndFlush();
        }

    }

    private static void ConfigureKestrelHttps(WebHostBuilderContext context, KestrelServerOptions options)
    {
        var httpsEndpointUrl = context.Configuration["Kestrel:Endpoints:Https:Url"];
        if (string.IsNullOrWhiteSpace(httpsEndpointUrl))
        {
            return;
        }

        var explicitCertificatePath = context.Configuration["Kestrel:Certificates:Default:Path"];
        if (!string.IsNullOrWhiteSpace(explicitCertificatePath) && File.Exists(explicitCertificatePath))
        {
            return;
        }

        _generatedHttpsCertificate ??= CreateSelfSignedHttpsCertificate();
        options.ConfigureHttpsDefaults(httpsOptions =>
        {
            httpsOptions.ServerCertificate = _generatedHttpsCertificate;
        });

        Log.Information("No HTTPS certificate was configured for Kestrel. An application-generated self-signed certificate will be used for the HTTPS endpoint.");
    }

    private static X509Certificate2 CreateSelfSignedHttpsCertificate()
    {
        using var rsa = RSA.Create(2048);
        var request = new CertificateRequest(
            "CN=PenguinTwitchBot",
            rsa,
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1);

        var sanBuilder = new SubjectAlternativeNameBuilder();
        sanBuilder.AddDnsName("localhost");
        sanBuilder.AddIpAddress(IPAddress.Loopback);
        sanBuilder.AddIpAddress(IPAddress.IPv6Loopback);

        foreach (var address in GetLocalIpv4Addresses())
        {
            sanBuilder.AddIpAddress(address);
        }

        request.CertificateExtensions.Add(sanBuilder.Build());
        request.CertificateExtensions.Add(new X509BasicConstraintsExtension(false, false, 0, false));
        request.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.KeyEncipherment, false));
        request.CertificateExtensions.Add(new X509SubjectKeyIdentifierExtension(request.PublicKey, false));

        using var certificate = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddYears(5));
        return X509CertificateLoader.LoadPkcs12(certificate.Export(X509ContentType.Pfx), ReadOnlySpan<char>.Empty, X509KeyStorageFlags.DefaultKeySet);
    }

    private static IEnumerable<IPAddress> GetLocalIpv4Addresses()
    {
        foreach (var networkInterface in NetworkInterface.GetAllNetworkInterfaces())
        {
            if (networkInterface.OperationalStatus != OperationalStatus.Up)
            {
                continue;
            }

            var ipProperties = networkInterface.GetIPProperties();
            foreach (var unicastAddress in ipProperties.UnicastAddresses)
            {
                if (unicastAddress.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork &&
                    !IPAddress.IsLoopback(unicastAddress.Address))
                {
                    yield return unicastAddress.Address;
                }
            }
        }
    }

    private static void RegisterGlobalExceptionHandlers()
    {
        TaskScheduler.UnobservedTaskException += (_, eventArgs) =>
        {
            if (IsExpectedBackgroundConnectionRefusal(eventArgs.Exception) ||
                IsExpectedBlazorInteropRegistrationRace(eventArgs.Exception))
            {
                Log.Debug(eventArgs.Exception, "Observed expected unobserved task exception during transient shutdown/reconnect flow");
                eventArgs.SetObserved();
                return;
            }
            else
            {
                Log.Error(eventArgs.Exception, "Unobserved task exception detected");
            }
            eventArgs.SetObserved();
        };

        AppDomain.CurrentDomain.ProcessExit += (_, _) =>
        {
            if (Interlocked.Increment(ref _fatalSignalCount) == 1)
            {
                Log.Information("Process exit signal received");
            }
            // CloseAndFlush is handled in the finally block after RunAsync.
        };

        Console.CancelKeyPress += (_, eventArgs) =>
        {
            if (Interlocked.Increment(ref _fatalSignalCount) == 1)
            {
                Log.Warning("Console cancel signal received ({SpecialKey})", eventArgs.SpecialKey);
            }
            // CloseAndFlush is handled in the finally block after RunAsync.
        };
    }

    private static void PrintSetupRequiredBanner(string secretsPath)
    {
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine();
        Console.WriteLine("╔══════════════════════════════════════════════════════╗");
        Console.WriteLine("║          DOTNET TWITCHBOT - SETUP REQUIRED          ║");
        Console.WriteLine("╠══════════════════════════════════════════════════════╣");
        Console.WriteLine($"║  Config file not found: {secretsPath,-29}║");
        Console.WriteLine("║                                                      ║");
        Console.WriteLine("║  Run PenguinTwitchBot.Setup.exe first to create     ║");
        Console.WriteLine("║  your configuration file, then restart the bot.     ║");
        Console.WriteLine("╚══════════════════════════════════════════════════════╝");
        Console.WriteLine();
        Console.ResetColor();
        Console.Write("Press any key to exit...");
    }

    private static bool IsExpectedBackgroundConnectionRefusal(Exception exception)    {
        var aggregate = exception as AggregateException;
        var all = aggregate?.Flatten().InnerExceptions ?? [exception];

        foreach (var inner in all)
        {
            var message = inner.ToString();
            if (message.Contains("Failed to start Websocket client", StringComparison.OrdinalIgnoreCase) &&
                message.Contains("Unable to connect to the remote server", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (message.Contains("actively refused", StringComparison.OrdinalIgnoreCase) &&
                message.Contains("4455", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsExpectedBlazorInteropRegistrationRace(Exception exception)
    {
        var aggregate = exception as AggregateException;
        var all = aggregate?.Flatten().InnerExceptions ?? [exception];

        foreach (var inner in all)
        {
            var message = inner.ToString();
            if (message.Contains("Interop methods are already registered for renderer", StringComparison.OrdinalIgnoreCase) &&
                message.Contains("attachWebRendererInterop", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static async Task RunStartupInitializationAsync(WebApplication app, ILogger<Program>? programLogger)
    {
        var url = app.Urls.FirstOrDefault()?.TrimEnd('/')
            ?? app.Configuration["Kestrel:Endpoints:Http:Url"]?.TrimEnd('/')
            ?? "http://localhost:5000";

        if (Uri.TryCreate(url, UriKind.Absolute, out var parsedUrl))
        {
            if (parsedUrl.Host == "0.0.0.0" || parsedUrl.Host == "[::]" || parsedUrl.Host == "::")
            {
                var port = parsedUrl.IsDefaultPort ? "" : $":{parsedUrl.Port}";
                url = $"{parsedUrl.Scheme}://localhost{port}";
            }
        }

        programLogger?.LogInformation("Bot Started");
        programLogger?.LogInformation("Connect to the bot at {Url}", url);

        var appLifetime = app.Services.GetRequiredService<IHostApplicationLifetime>();
        _ = Task.Factory.StartNew(async () =>
        {
            try
            {
                // Defer optional startup cleanup so the bot can become responsive first.
                await Task.Delay(TimeSpan.FromSeconds(10), appLifetime.ApplicationStopping);
                await RunConfiguredStartupCleanupJobsAsync(app.Services, programLogger, appLifetime.ApplicationStopping);
            }
            catch (OperationCanceledException)
            {
                // App is stopping; no cleanup needed.
            }
            catch (Exception ex)
            {
                programLogger?.LogError(ex, "Startup cleanup task failed.");
            }
        }, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default).Unwrap();

        await Task.CompletedTask;
    }

    private static async Task RunConfiguredStartupCleanupJobsAsync(
        IServiceProvider serviceProvider,
        ILogger<Program>? programLogger,
        CancellationToken stoppingToken)
    {
        using var scope = serviceProvider.CreateScope();
        var settings = scope.ServiceProvider.GetRequiredService<IScheduledJobSettingsService>();
        var fileCleanupService = scope.ServiceProvider.GetRequiredService<IFileCleanupService>();
        var chatHistory = scope.ServiceProvider.GetRequiredService<IChatHistory>();
        var ipLog = scope.ServiceProvider.GetRequiredService<PenguinTwitchBot.Circuit.IpLog>();
        var cooldownCleanupService = scope.ServiceProvider.GetRequiredService<ICooldownCleanupService>();

        stoppingToken.ThrowIfCancellationRequested();
        if (await settings.GetJobEnabledAsync(IScheduledJobSettingsService.CleanupChatLogsJobName, true) &&
            await settings.GetRunOnStartupAsync(IScheduledJobSettingsService.CleanupChatLogsJobName, true))
        {
            programLogger?.LogInformation("Running startup cleanup: chat logs");
            await chatHistory.CleanOldLogs();
        }

        stoppingToken.ThrowIfCancellationRequested();
        if (await settings.GetJobEnabledAsync(IScheduledJobSettingsService.CleanupIpLogsJobName, true) &&
            await settings.GetRunOnStartupAsync(IScheduledJobSettingsService.CleanupIpLogsJobName, true))
        {
            programLogger?.LogInformation("Running startup cleanup: IP logs");
            await ipLog.CleanupOldIpLogs();
        }

        stoppingToken.ThrowIfCancellationRequested();
        if (await settings.GetJobEnabledAsync(IScheduledJobSettingsService.CleanupCooldownsJobName, true) &&
            await settings.GetRunOnStartupAsync(IScheduledJobSettingsService.CleanupCooldownsJobName, true))
        {
            programLogger?.LogInformation("Running startup cleanup: expired cooldowns");
            await cooldownCleanupService.CleanupExpiredCooldownsAsync();
        }

        stoppingToken.ThrowIfCancellationRequested();
        if (await settings.GetJobEnabledAsync(IScheduledJobSettingsService.TtsCleanupJobName, true) &&
            await settings.GetRunOnStartupAsync(IScheduledJobSettingsService.TtsCleanupJobName, true))
        {
            programLogger?.LogInformation("Running startup cleanup: TTS files");
            await fileCleanupService.CleanupTtsAsync();
        }

        stoppingToken.ThrowIfCancellationRequested();
        if (await settings.GetJobEnabledAsync(IScheduledJobSettingsService.CleanupClipsJobName, true) &&
            await settings.GetRunOnStartupAsync(IScheduledJobSettingsService.CleanupClipsJobName, true))
        {
            programLogger?.LogInformation("Running startup cleanup: clip files");
            await fileCleanupService.CleanupClipsAsync();
        }
        programLogger?.LogInformation("Startup cleanup completed.");
    }

    private static async Task ConfigureQuartzTriggersAsync(IServiceProvider serviceProvider)
    {
        using var scope = serviceProvider.CreateScope();
        var settings = scope.ServiceProvider.GetRequiredService<IScheduledJobSettingsService>();
        var schedulerFactory = scope.ServiceProvider.GetRequiredService<ISchedulerFactory>();
        var scopedLogger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
        var scheduler = await schedulerFactory.GetScheduler();

        var defaultCronByJob = new Dictionary<string, string>
        {
            [IScheduledJobSettingsService.TriggerBackupJobName] = "0 0 12 * * ?",
            [IScheduledJobSettingsService.CleanupClipsJobName] = "0 0 12 * * ?",
            [IScheduledJobSettingsService.CleanupChatLogsJobName] = "0 0 13 * * ?",
            [IScheduledJobSettingsService.CleanupIpLogsJobName] = "0 30 13 * * ?",
            [IScheduledJobSettingsService.CleanupCooldownsJobName] = "0 0/30 * * * ?",
            [IScheduledJobSettingsService.TtsCleanupJobName] = "0 0 * * * ?",
            [IScheduledJobSettingsService.UpdateDiscordEventsJobName] = "0 0/5 * * * ?",
            [IScheduledJobSettingsService.PostScheduleJobName] = "0 0 9 ? * MON",
            [IScheduledJobSettingsService.UpdatePostedScheduleJobName] = "0 0 10 * * ?",
            [IScheduledJobSettingsService.ValidationSanityCheckJobName] = "0 0 5 * * ?"
        };

        foreach (var kvp in defaultCronByJob)
        {
            var enabled = await settings.GetJobEnabledAsync(kvp.Key, true);
            var configuredCron = await settings.GetJobCronAsync(kvp.Key, kvp.Value);
            var cronToUse = CronExpression.IsValidExpression(configuredCron) ? configuredCron : kvp.Value;
            if (!CronExpression.IsValidExpression(configuredCron))
            {
                scopedLogger.LogWarning("Invalid cron '{Cron}' for job {JobName}. Falling back to default '{DefaultCron}'.", configuredCron, kvp.Key, kvp.Value);
                await settings.SetJobCronAsync(kvp.Key, kvp.Value);
            }

            await UpsertCronTriggerAsync(scheduler, new JobKey(kvp.Key), new TriggerKey($"{kvp.Key}-Trigger"), enabled, cronToUse);
        }
    }

    private static async Task UpsertCronTriggerAsync(
        IScheduler scheduler,
        JobKey jobKey,
        TriggerKey triggerKey,
        bool enabled,
        string cronExpression)
    {
        var existingTrigger = await scheduler.GetTrigger(triggerKey);
        if (existingTrigger != null)
        {
            await scheduler.UnscheduleJob(triggerKey);
        }

        if (!enabled)
        {
            return;
        }

        var trigger = TriggerBuilder.Create()
            .WithIdentity(triggerKey)
            .ForJob(jobKey)
            .WithCronSchedule(cronExpression)
            .Build();

        await scheduler.ScheduleJob(trigger);
    }

    private static bool IsExpectedTransientBlazorException(Exception? exception, IReadOnlyDictionary<string, LogEventPropertyValue> properties)
    {
        if (exception is null || !properties.TryGetValue("SourceContext", out var sourceContext))
        {
            return false;
        }

        var context = sourceContext.ToString().Trim('"');
        var isBlazorCircuitContext = context.StartsWith("Microsoft.AspNetCore.Components.Server.Circuits.RemoteNavigationManager", StringComparison.Ordinal) ||
                                     context.StartsWith("Microsoft.AspNetCore.Components.Server.Circuits.CircuitHost", StringComparison.Ordinal) ||
                                     context.StartsWith("Microsoft.AspNetCore.Components.Server.ComponentHub", StringComparison.Ordinal) ||
                                     context.StartsWith("Microsoft.AspNetCore.Components.Server.Circuits.RemoteJSRuntime", StringComparison.Ordinal);

        if (!isBlazorCircuitContext)
        {
            return false;
        }

        var aggregate = exception as AggregateException;
        var all = aggregate?.Flatten().InnerExceptions ?? [exception];

        foreach (var inner in all)
        {
            if (inner is TaskCanceledException ||
                inner is OperationCanceledException ||
                inner is ObjectDisposedException ||
                inner is Microsoft.JSInterop.JSDisconnectedException)
            {
                continue;
            }

            var message = inner.ToString();
            if (message.Contains("circuit has disconnected and is being disposed", StringComparison.OrdinalIgnoreCase) ||
                message.Contains("JavaScript interop calls cannot be issued at this time", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            return false; // at least one non-transient exception — don't suppress
        }

        return true; // all inner exceptions are expected transient types
    }

    private static void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is Exception ex)
        {
            Log.Fatal(ex, "Unhandled exception (IsTerminating: {IsTerminating})", e.IsTerminating);
        }
        else
        {
            Log.Fatal("Unhandled non-exception object (IsTerminating: {IsTerminating}): {ExceptionObject}", e.IsTerminating, e.ExceptionObject);
        }

        Log.CloseAndFlush();
    }

    private static string GetDatabaseProvider(IConfiguration configuration)
    {
        // Check environment variable first (for debug profile selection)
        var envProvider = Environment.GetEnvironmentVariable("DATABASE_PROVIDER")?.Trim().ToLowerInvariant();
        if (!string.IsNullOrEmpty(envProvider))
        {
            Log.Information("Using database provider {Provider} from environment variable", envProvider);
            return envProvider switch
            {
                "postgres" or "postgresql" => "postgres",
                "sqlite" => "sqlite",
                _ => throw new InvalidOperationException($"Unsupported database provider '{envProvider}'. Supported values: postgres, postgresql, sqlite.")
            };
        }

        // Fall back to configuration file
        var provider = configuration.GetValue<string>("Database:Provider")?.Trim().ToLowerInvariant();
        return provider switch
        {
            null or "" => "sqlite",
            "postgres" => "postgres",
            "postgresql" => "postgres",
            "sqlite" => "sqlite",
            _ => throw new InvalidOperationException($"Unsupported database provider '{provider}'. Supported values: postgres, postgresql, sqlite.")
        };
    }

    private static void PreloadMigrationAssemblies()
    {
        var baseDir = AppContext.BaseDirectory;
        string[] migrationDlls =
        [
            "PenguinTwitchBot.Migrations.Postgres.dll",
            "PenguinTwitchBot.Migrations.Sqlite.dll",
        ];
        foreach (var dll in migrationDlls)
        {
            var path = Path.Combine(baseDir, dll);
            if (File.Exists(path))
                System.Reflection.Assembly.LoadFrom(path);
        }
    }

    private static string GetMigrationsAssembly(string provider)
    {
        return provider switch
        {
            "postgres" => "PenguinTwitchBot.Migrations.Postgres",
            "sqlite" => "PenguinTwitchBot.Migrations.Sqlite",
            _ => throw new InvalidOperationException($"Unsupported database provider '{provider}'.")
        };
    }

    private static string GetConnectionString(IConfiguration configuration, string provider)
    {
        var configured = provider switch
        {
            "postgres" => configuration.GetConnectionString("PostgresConnection"),
            "sqlite" => string.IsNullOrEmpty(configuration.GetConnectionString("SqliteConnection"))
                ? $"Data Source={Path.Combine(AppContext.BaseDirectory, "Data", "PenguinTwitchBot.sqlite")}"
                : configuration.GetConnectionString("SqliteConnection"),
            _ => null
        };

        return string.IsNullOrWhiteSpace(configured)
            ? throw new InvalidOperationException($"Connection string is missing for provider '{provider}'.")
            : configured;
    }

    private static void ConfigureDbContextOptions(DbContextOptionsBuilder options, string provider, string connectionString, string migrationsAssembly)
    {
        // Downgrade pending-model-changes from error to warning so the app can start
        // while a migration is being prepared.
        options.ConfigureWarnings(w =>
        {
            w.Log(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning);

            // SQLite emits this warning when it needs PRAGMA foreign_keys toggles around
            // schema updates. This is expected for provider-generated migration SQL.
            if (provider == "sqlite")
            {
                w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.NonTransactionalMigrationOperationWarning);
            }
        });

        switch (provider)
        {
            case "postgres":
                options.UseNpgsql(connectionString, postgresOptions =>
                {
                    postgresOptions.MigrationsAssembly(migrationsAssembly)
                        .EnableRetryOnFailure(
                            maxRetryCount: 5,
                            maxRetryDelay: TimeSpan.FromSeconds(15),
                            errorCodesToAdd: null
                        );
                });
                break;

            case "sqlite":
                // Append shared-cache, foreign keys, and pooling defaults if not already specified
                var sqliteConnStr = connectionString!.Contains("Cache=", StringComparison.OrdinalIgnoreCase)
                    ? connectionString
                    : connectionString + ";Cache=Shared;Foreign Keys=True";
                options.UseSqlite(sqliteConnStr, sqliteOptions =>
                {
                    sqliteOptions.MigrationsAssembly(migrationsAssembly)
                        .CommandTimeout(30);
                });
                options.AddInterceptors(new SqliteWalInterceptor());
                break;

            default:
                throw new InvalidOperationException($"Unsupported database provider '{provider}'.");
        }
    }
}

/// <summary>
/// Applies WAL journal mode and busy timeout PRAGMAs to every SQLite connection opened by EF Core.
/// Required for multi-threaded ASP.NET Core workloads to avoid "database is locked" errors.
/// </summary>
file sealed class SqliteWalInterceptor : Microsoft.EntityFrameworkCore.Diagnostics.DbConnectionInterceptor
{
    private const string Pragmas = "PRAGMA journal_mode=WAL; PRAGMA busy_timeout=5000;";

    public override void ConnectionOpened(
        System.Data.Common.DbConnection connection,
        Microsoft.EntityFrameworkCore.Diagnostics.ConnectionEndEventData eventData)
    {
        using var cmd = connection.CreateCommand();
        cmd.CommandText = Pragmas;
        cmd.ExecuteNonQuery();
    }

    public override async Task ConnectionOpenedAsync(
        System.Data.Common.DbConnection connection,
        Microsoft.EntityFrameworkCore.Diagnostics.ConnectionEndEventData eventData,
        CancellationToken cancellationToken = default)
    {
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = Pragmas;
        await cmd.ExecuteNonQueryAsync(cancellationToken);
    }
}