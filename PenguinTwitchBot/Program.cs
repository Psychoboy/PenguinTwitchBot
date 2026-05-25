global using PenguinTwitchBot.Bot.Core.Database;
global using PenguinTwitchBot.Bot.Models;
global using Microsoft.EntityFrameworkCore;
global using System.ComponentModel.DataAnnotations;
global using System.ComponentModel.DataAnnotations.Schema;
using PenguinTwitchBot.Application.Notifications;
using PenguinTwitchBot.Bot.Core;
using PenguinTwitchBot.Bot.TwitchServices;
using PenguinTwitchBot.Circuit;
using PenguinTwitchBot.CustomMiddleware;
using PenguinTwitchBot.HealthChecks;
using PenguinTwitchBot.Repository;
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
using System.Collections;
using System.Diagnostics;
using System.Net;
using System.Net.NetworkInformation;
using TwitchLib.EventSub.Websockets.Extensions;
internal class Program
{
    private static ILogger<Program>? logger;
    private static int _fatalSignalCount;

    private static async Task Main(string[] args)
    {
        // Pre-load migration assemblies from the application directory so Assembly.Load(name)
        // can find them in self-contained / single-file publish scenarios.
        PreloadMigrationAssemblies();

        Environment.SetEnvironmentVariable("OTEL_DOTNET_AUTO_INSTRUMENTATION_ENABLED", "true");
        Environment.SetEnvironmentVariable("OTEL_SERVICE_NAME", "PenguinTwitchBot");
        using var server = new Prometheus.KestrelMetricServer(port: 4999);
        server.Start();
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
        Activity.DefaultIdFormat = System.Diagnostics.ActivityIdFormat.W3C;
        var readerOptions = new Serilog.Settings.Configuration.ConfigurationReaderOptions(
            typeof(ConsoleLoggerConfigurationExtensions).Assembly,
            typeof(Serilog.Expressions.SerilogExpression).Assembly,
            typeof(Serilog.RollingInterval).Assembly,
            typeof(Serilog.Sinks.OpenTelemetry.OtlpProtocol).Assembly
        );
        var loggerConfiguration = new LoggerConfiguration()
           .ReadFrom.Configuration(builder.Configuration, readerOptions)
           .Enrich.WithSpan()
           .Enrich.FromLogContext()
           .Filter.ByExcluding(logEvent =>
           {
               return IsExpectedTransientBlazorException(logEvent.Exception, logEvent.Properties);
           })
           .WriteTo.OpenTelemetry(options =>
           {
               options.Endpoint = "http://localhost:4318"; // Adjust the endpoint as needed
               options.Protocol = Serilog.Sinks.OpenTelemetry.OtlpProtocol.HttpProtobuf; // Use the appropriate protocol
               options.IncludedData = IncludedData.MessageTemplateTextAttribute |
                                      IncludedData.TraceIdField | IncludedData.SpanIdField |
                                      IncludedData.SpecRequiredResourceAttributes; 
               options.ResourceAttributes = new Dictionary<string, object>
               {
                   { "service.name", "PenguinTwitchBot" },
                   { "service.environment", builder.Environment.EnvironmentName }
               };
           });

        var serilogLogger = loggerConfiguration.CreateLogger();
        Log.Logger = serilogLogger;

        builder.Logging.ClearProviders();
        builder.Logging.AddSerilog(serilogLogger, dispose: false);

        RegisterGlobalExceptionHandlers();

        // Add OpenTelemetry data to json logs
        builder.Services.AddOpenTelemetry()
            .ConfigureResource(resource => resource.AddService("PenguinTwitchBot"))
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

        //Database
        builder.Services.AddSingleton<IDatabaseTools, DatabaseTools>();
        builder.Services.AddTwitchLibEventSubWebsockets();
        builder.Services.AddPenguinDispatcher(typeof(Program).Assembly);
        builder.Services.AddBotCommands();




        builder.Services.AddQuartz(q =>
        {
            var backupDbJobKey = new JobKey("BackupDbJob");
            var updateDiscordEventsKey = new JobKey("UpdateDiscordEvents");
            var postScheduleKey = new JobKey("PostSchedule");
            var cleanupChatLogs = new JobKey("CleanUpChatLogs");
            var hourlyCLeanup = new JobKey("HourlyCleanup");
            var updatePostedSchedulekey = new JobKey("UpdatePostedSchedule");
            q.AddJob<PenguinTwitchBot.Bot.ScheduledJobs.BackupDbJob>(opts => opts.WithIdentity(backupDbJobKey));
            q.AddTrigger(opts => opts
                .ForJob(backupDbJobKey)
                .WithIdentity("BackupDb-Trigger")
                .WithCronSchedule(CronScheduleBuilder.DailyAtHourAndMinute(12, 00)) //Every day at noon
            );

            q.AddJob<PenguinTwitchBot.Bot.ScheduledJobs.UpdateDiscordEvents>(opts => opts.WithIdentity(updateDiscordEventsKey));
            q.AddTrigger(opts => opts
                .ForJob(updateDiscordEventsKey)
                .WithIdentity("UpdateDiscord-Trigger")
                .WithCronSchedule(CronScheduleBuilder.CronSchedule("0 0/5 * * * ?"))
            );

            q.AddJob<PenguinTwitchBot.Bot.ScheduledJobs.PostSchedule>(opts => opts.WithIdentity(postScheduleKey));
            q.AddTrigger(opts => opts
                .ForJob(postScheduleKey)
                .WithIdentity("UpdateDiscordSchedule-Trigger")
                .WithCronSchedule(CronScheduleBuilder.WeeklyOnDayAndHourAndMinute(DayOfWeek.Monday, 9, 0))
                );

            q.AddJob<PenguinTwitchBot.Bot.ScheduledJobs.CleanupChatLogJob>(opts => opts.WithIdentity(cleanupChatLogs));
            q.AddTrigger(opts => opts
                .ForJob(cleanupChatLogs)
                .WithIdentity("CleanUpChatLogs-Trigger")
                .WithCronSchedule(CronScheduleBuilder.DailyAtHourAndMinute(13, 00)) //Every day at 1PM
            );
            q.AddJob<PenguinTwitchBot.Bot.ScheduledJobs.HourlyCleanupJob>(opts => opts.WithIdentity(hourlyCLeanup));
            q.AddTrigger(opts => opts
                .ForJob(hourlyCLeanup)
                .WithIdentity("HourlyCleanup-Trigger")
                .WithSimpleSchedule(x => x.WithIntervalInHours(1).RepeatForever().Build()) //Every Hour
            );
            q.AddJob<PenguinTwitchBot.Bot.ScheduledJobs.UpdatePostedSchedule>(opts => opts.WithIdentity(updatePostedSchedulekey));
            q.AddTrigger(opts => opts
                .ForJob(updatePostedSchedulekey)
                .WithIdentity("UpdatePostedSchedule-Trigger")
                .WithCronSchedule(CronScheduleBuilder.DailyAtHourAndMinute(10, 00)) //Every day at 10AM
            );

            var validationSanityCheckKey = new JobKey("ValidationSanityCheck");
            q.AddJob<PenguinTwitchBot.Bot.ScheduledJobs.ValidationSanityCheckJob>(opts => opts.WithIdentity(validationSanityCheckKey));
            q.AddTrigger(opts => opts
                .ForJob(validationSanityCheckKey)
                .WithIdentity("ValidationSanityCheck-Trigger")
                .WithCronSchedule(CronScheduleBuilder.DailyAtHourAndMinute(5, 00)) //Every day at 5AM
            );
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
        builder.Services.AddScoped<PenguinTwitchBot.Services.ImageProcessingService>();
        builder.Services.AddScoped<PenguinTwitchBot.Services.DiscordLookupService>();
        builder.Services.AddHttpClient("GitHubRelease", c =>
        {
            c.DefaultRequestHeaders.UserAgent.ParseAdd("PenguinTwitchBot");
        });
        builder.Services.AddSingleton<PenguinTwitchBot.Services.VersionCheckService>();
        builder.Services.AddSingleton<PenguinTwitchBot.Services.IVersionCheckService>(sp =>
            sp.GetRequiredService<PenguinTwitchBot.Services.VersionCheckService>());
        builder.Services.AddHostedService(sp =>
            sp.GetRequiredService<PenguinTwitchBot.Services.VersionCheckService>());

        builder.Services.Configure<ForwardedHeadersOptions>(options =>
        {
            options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
            options.RequireHeaderSymmetry = false;
            options.ForwardLimit = null;
            options.KnownIPNetworks.Add(System.Net.IPNetwork.Parse("10.0.0.0/8"));
            options.KnownIPNetworks.Add(System.Net.IPNetwork.Parse("172.16.0.0/12"));
            options.KnownIPNetworks.Add(System.Net.IPNetwork.Parse("192.168.0.0/16"));
        });

        var app = builder.Build();
        app.UseAuthentication();
        app.UseAuthorization();
        app.UseForwardedHeaders();

        // Always migrate — for SQLite this creates the database file and schema on first run.
        // For MariaDB/Postgres the server must be reachable before starting the app.
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

        app.UseMiddleware<PenguinTwitchBot.CustomMiddleware.ErrorHandlerMiddleware>();

        // Configure the HTTP request pipeline.
        if (!app.Environment.IsDevelopment())
        {
            app.UseExceptionHandler("/Error");
            // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
            //app.UseHsts();

        }

        app.UseStatusCodePagesWithReExecute("/NotFoundItem", "?statusCode={0}");

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
            var url = app.Configuration["BaseUrl"] ?? "http://localhost:5000";
            logger?.LogInformation("Bot Started");
            logger?.LogInformation("Connect to the bot at {Url}", url);
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
            if (!File.Exists("yt-dlp.exe"))
            {
                await YoutubeDLSharp.Utils.DownloadYtDlp();
                await YoutubeDLSharp.Utils.DownloadFFmpeg();
            }
        }
        catch (Exception) { }

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
            Log.CloseAndFlush();
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
                return true;
            }

            var message = inner.ToString();
            if (message.Contains("circuit has disconnected and is being disposed", StringComparison.OrdinalIgnoreCase) ||
                message.Contains("JavaScript interop calls cannot be issued at this time", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
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
                "mariadb" or "mysql" => "mariadb",
                "postgres" or "postgresql" => "postgres",
                "sqlite" => "sqlite",
                _ => throw new InvalidOperationException($"Unsupported database provider '{envProvider}'. Supported values: mariadb, mysql, postgres, postgresql, sqlite.")
            };
        }

        // Fall back to configuration file
        var provider = configuration.GetValue<string>("Database:Provider")?.Trim().ToLowerInvariant();
        return provider switch
        {
            null or "" => "sqlite",
            "mariadb" => "mariadb",
            "mysql" => "mariadb",
            "postgres" => "postgres",
            "postgresql" => "postgres",
            "sqlite" => "sqlite",
            _ => throw new InvalidOperationException($"Unsupported database provider '{provider}'. Supported values: mariadb, mysql, postgres, postgresql, sqlite.")
        };
    }

    private static void PreloadMigrationAssemblies()
    {
        var baseDir = AppContext.BaseDirectory;
        string[] migrationDlls =
        [
            "PenguinTwitchBot.Migrations.Postgres.dll",
            "PenguinTwitchBot.Migrations.MariaDb.dll",
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
            "mariadb" => "PenguinTwitchBot.Migrations.MariaDb",
            "postgres" => "PenguinTwitchBot.Migrations.Postgres",
            "sqlite" => "PenguinTwitchBot.Migrations.Sqlite",
            _ => throw new InvalidOperationException($"Unsupported database provider '{provider}'.")
        };
    }

    private static string GetConnectionString(IConfiguration configuration, string provider)
    {
        var configured = provider switch
        {
            "mariadb" => configuration.GetConnectionString("MariaDbConnection") ?? configuration.GetConnectionString("DefaultConnection"),
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
            w.Log(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning));

        switch (provider)
        {
            case "mariadb":
                options.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString), mysqlOptions =>
                {
                    mysqlOptions.MigrationsAssembly(migrationsAssembly)
                        .EnableRetryOnFailure(
                            maxRetryCount: 5,
                            maxRetryDelay: TimeSpan.FromSeconds(15),
                            errorNumbersToAdd: null
                        )
                        .TranslateParameterizedCollectionsToConstants()
                        .EnablePrimitiveCollectionsSupport();
                });
                break;

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