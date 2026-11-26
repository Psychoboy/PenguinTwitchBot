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
using System.Diagnostics;
using System.IO.Abstractions;
using System.Security.Cryptography.X509Certificates;

internal partial class Program
{
    private static ILogger<Program>? logger;
    private static int _fatalSignalCount;
    private static X509Certificate2? _generatedHttpsCertificate;

    private static async Task Main(string[] args)
    {
        // Pre-load migration assemblies from the application directory so Assembly.Load(name)
        // can find them in self-contained / single-file publish scenarios.
        PreloadMigrationAssemblies();

        var cliSkipBackup = args.Any(a => string.Equals(a, "--skip-database-backup", StringComparison.OrdinalIgnoreCase) ||
                                          string.Equals(a, "--skip-db-backup", StringComparison.OrdinalIgnoreCase));
        var filteredArgs = args.Where(a => !string.Equals(a, "--skip-database-backup", StringComparison.OrdinalIgnoreCase) &&
                                           !string.Equals(a, "--skip-db-backup", StringComparison.OrdinalIgnoreCase)).ToArray();

        var builder = WebApplication.CreateBuilder(filteredArgs);
        var skipBackupEnv = builder.Configuration["SKIP_DATABASE_BACKUP"] ?? builder.Configuration["SKIP_DB_BACKUP"];
        if (!string.IsNullOrEmpty(skipBackupEnv))
        {
            builder.Configuration["SkipDatabaseBackup"] = skipBackupEnv;
        }

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

        if (cliSkipBackup)
        {
            builder.Configuration["SkipDatabaseBackup"] = "true";
        }

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
                options.LevelSwitch = new Serilog.Core.LoggingLevelSwitch(LogEventLevel.Information);
            });
            Log.Information("OpenTelemetry logging enabled. Exporting logs to {Endpoint}", otelEndpoint);
        }

        var serilogLogger = loggerConfiguration.CreateLogger();
        Log.Logger = serilogLogger;

        builder.Logging.ClearProviders();
        builder.Logging.AddSerilog(serilogLogger, dispose: false);

        RegisterGlobalExceptionHandlers();

        if (otelEnabled && !string.IsNullOrEmpty(otelEndpoint))
        {
            var traceEndpoint = BuildOtlpHttpSignalEndpoint(otelEndpoint, "traces");
            var metricEndpoint = BuildOtlpHttpSignalEndpoint(otelEndpoint, "metrics");
            builder.Services.AddOpenTelemetry()
                .ConfigureResource(resource => resource.AddService("PenguinTwitchBot"))
                .WithTracing(tracing => tracing
                    .AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation()
                    .AddOtlpExporter(otlp =>
                    {
                        otlp.Endpoint = traceEndpoint;
                        otlp.Protocol = OpenTelemetry.Exporter.OtlpExportProtocol.HttpProtobuf;
                    }))
                .WithMetrics(metrics => metrics
                    .AddAspNetCoreInstrumentation()
                    .AddOtlpExporter(otlp =>
                    {
                        otlp.Endpoint = metricEndpoint;
                        otlp.Protocol = OpenTelemetry.Exporter.OtlpExportProtocol.HttpProtobuf;
                    }));
            Log.Information("OpenTelemetry tracing and metrics enabled. Exporting traces to {TraceEndpoint} and metrics to {MetricEndpoint}", traceEndpoint, metricEndpoint);
        }

        // Add services to the container.
        builder.Services.AddControllersWithViews();
        builder.Services.AddSingleton<SettingsFileManager>();
        builder.Services.AddSingleton<ILanguage, Language>();

        builder.Services.AddSingleton<PenguinTwitchBot.Circuit.SessionRevocationService>();
        builder.Services.AddSingleton<PenguinTwitchBot.Circuit.ISessionRevocationService>(sp => sp.GetRequiredService<PenguinTwitchBot.Circuit.SessionRevocationService>());
        builder.Services.AddHostedService(sp => sp.GetRequiredService<PenguinTwitchBot.Circuit.SessionRevocationService>());
        builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.ExpireTimeSpan = TimeSpan.FromDays(30);
        options.SlidingExpiration = true;
        options.Events.OnValidatePrincipal = async context =>
        {
            var revocations = context.HttpContext.RequestServices.GetRequiredService<PenguinTwitchBot.Circuit.ISessionRevocationService>();
            if (revocations.IsRevoked(context.Principal))
            {
                context.RejectPrincipal();
                await Microsoft.AspNetCore.Authentication.AuthenticationHttpContextExtensions.SignOutAsync(context.HttpContext, CookieAuthenticationDefaults.AuthenticationScheme);
            }
        };
    });

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
            var refreshEmoteCacheKey = new JobKey(IScheduledJobSettingsService.RefreshEmoteCacheJobName);

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
            q.AddJob<PenguinTwitchBot.Bot.ScheduledJobs.RefreshEmoteCacheJob>(opts => opts.WithIdentity(refreshEmoteCacheKey).StoreDurably());
        });
        builder.Services.AddQuartzHostedService(
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
        builder.Services.AddSingleton<PenguinTwitchBot.Bot.Hubs.IInProcessHubEventBus, PenguinTwitchBot.Bot.Hubs.InProcessHubEventBus>();
        PenguinTwitchBot.Bot.Hubs.InProcessHubBridgeServiceCollectionExtensions.AddInProcessHubBridge<PenguinTwitchBot.Bot.Hubs.MainHub>(builder.Services);
        PenguinTwitchBot.Bot.Hubs.InProcessHubBridgeServiceCollectionExtensions.AddInProcessHubBridge<PenguinTwitchBot.Bot.Commands.Music.YtHub>(builder.Services);

        // Always register circuit services — MainLayout injects these and Blazor's SSR
        // prerender instantiates it even when the page specifies its own layout.
        builder.Services.AddSingleton<PenguinTwitchBot.Circuit.IpLog>();
        builder.Services.AddSingleton<PenguinTwitchBot.Circuit.IIpLog>(sp => sp.GetRequiredService<PenguinTwitchBot.Circuit.IpLog>());
        builder.Services.AddSingleton<ICircuitUserService, CircuitUserService>();
        builder.Services.AddSingleton<TwitchExtensionTrackingService>();
        builder.Services.AddSingleton<ITwitchExtensionTrackingService>(sp => sp.GetRequiredService<TwitchExtensionTrackingService>());
        builder.Services.AddHostedService(sp => sp.GetRequiredService<TwitchExtensionTrackingService>());
        builder.Services.AddScoped<CircuitHandler>((sp) =>
            new CircuitHandlerService(
                sp.GetRequiredService<ICircuitUserService>(),
                sp.GetRequiredService<ILogger<CircuitHandlerService>>()));

        builder.Configuration.GetRequiredSection("Discord").Get<DiscordSettings>();

        builder.Services.AddSingleton<PenguinTwitchBot.Bot.Ai.IOpenAiClientProvider, PenguinTwitchBot.Bot.Ai.OpenAiClientProvider>();
        builder.Services.AddScoped<PenguinTwitchBot.Bot.Ai.IStarCitizenAI, PenguinTwitchBot.Bot.Ai.StarCitizenAI>();
        builder.Services.AddScoped<PenguinTwitchBot.Bot.Ai.IShoutoutAi, PenguinTwitchBot.Bot.Ai.ShoutoutAi>();
        builder.Services.AddSingleton<PenguinTwitchBot.Bot.Ai.IOpenAiModelService, PenguinTwitchBot.Bot.Ai.OpenAiModelService>();
        builder.Services.AddScoped<PenguinTwitchBot.Bot.Ai.IOpenAiResponseService, PenguinTwitchBot.Bot.Ai.OpenAiResponseService>();
        builder.Services.AddScoped<PenguinTwitchBot.Bot.Ai.IOpenAiViewerContextService, PenguinTwitchBot.Bot.Ai.OpenAiViewerContextService>();
        builder.Services.AddRuntimeFeatureRegistration<PenguinTwitchBot.Bot.Ai.IOpenAiResponseService>(
            PenguinTwitchBot.Bot.Features.FeatureKeys.OpenAI,
            "OpenAI",
            isCore: false,
            description: "OpenAI integration for custom AI subactions and chat features.");

            builder.Services.AddHealthChecks()
                .AddCheck<TwitchBotHealthCheck>("TwitchChatBot")
                .AddCheck<CommandServiceHealthCheck>("ServiceBackbone")
                .AddCheck<DiscordServiceHealthCheck>("DiscordBot")
                .AddQuartz()
                .ForwardToPrometheus();
        builder.Services.AddScoped<BlazorAppContext>();
        builder.Services.AddHttpContextAccessor();
        builder.Services.AddSingleton<ICustomThemeService, CustomThemeService>();
        builder.Services.AddSingleton<IUserThemePreferenceService, UserThemePreferenceService>();
        builder.Services.AddSingleton<IThemeMetricsService, ThemeMetricsService>();
        builder.Services.AddScoped<IUserThemeService, UserThemeService>();
        builder.Services.AddScoped<PenguinTwitchBot.Services.HomepageLayoutService>();
        builder.Services.AddSingleton<PenguinTwitchBot.Services.AnnouncementService>();
        builder.Services.AddScoped<PenguinTwitchBot.Services.LeaderboardsLayoutService>();
        builder.Services.AddScoped<PenguinTwitchBot.Pages.Components.SubActionReferenceNames>();
        builder.Services.AddScoped<IBackupSettingsService, BackupSettingsService>();
        builder.Services.AddSingleton<IUpdateChannelSettingsService, UpdateChannelSettingsService>();
        builder.Services.AddSingleton<IChatHistoryRetentionSettingsService, ChatHistoryRetentionSettingsService>();
        builder.Services.AddSingleton<IIpLogRetentionSettingsService, IpLogRetentionSettingsService>();
        builder.Services.AddSingleton<IScheduledJobSettingsService, ScheduledJobSettingsService>();
        builder.Services.AddSingleton<ITTSSettingsService, TTSSettingsService>();
        builder.Services.AddSingleton<PenguinTwitchBot.Bot.Commands.TTS.IPiperService, PenguinTwitchBot.Bot.Commands.TTS.PiperService>();
        builder.Services.AddSingleton<ICooldownCleanupService, CooldownCleanupService>();
        builder.Services.AddSingleton<IFileCleanupService, FileCleanupService>();
        builder.Services.AddScoped<PenguinTwitchBot.Services.ImageProcessingService>();
        builder.Services.AddSingleton<PenguinTwitchBot.Services.MediaUploadService>();
        builder.Services.AddScoped<PenguinTwitchBot.Services.DiscordLookupService>();
        builder.Services.AddScoped<PenguinTwitchBot.Services.IIntegrationTestService, PenguinTwitchBot.Services.IntegrationTestService>();
        builder.Services.AddScoped<PenguinTwitchBot.Services.IQuickSearchService, PenguinTwitchBot.Services.QuickSearchService>();
        builder.Services.AddSingleton<PenguinTwitchBot.Services.IHelpContentService, PenguinTwitchBot.Services.HelpContentService>();
        builder.Services.AddHttpClient("GitHubRelease", c =>
        {
            c.DefaultRequestHeaders.UserAgent.ParseAdd("PenguinTwitchBot");
        });
        builder.Services.AddHttpClient("Emotes", c =>
        {
            c.DefaultRequestHeaders.UserAgent.ParseAdd("PenguinTwitchBot/1.0");
        }).ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
        {
            AllowAutoRedirect = false
        });
        // IMemoryCache is also added transitively by AddServerSideBlazor, but EmoteService
        // depends on it directly so register it explicitly rather than relying on that.
        builder.Services.AddMemoryCache();
        builder.Services.AddSingleton<PenguinTwitchBot.Services.IEmoteService, PenguinTwitchBot.Services.EmoteService>();

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

        builder.Services.AddCors(options =>
        {
            options.AddPolicy("TwitchExtensionCors", policy =>
            {
                policy.SetIsOriginAllowed(origin =>
                    origin.EndsWith(".ext-twitch.tv", StringComparison.OrdinalIgnoreCase) ||
                    origin.EndsWith(".twitch.tv", StringComparison.OrdinalIgnoreCase) ||
                    origin.Equals("https://twitch.tv", StringComparison.OrdinalIgnoreCase) ||
                    origin.StartsWith("https://localhost:", StringComparison.OrdinalIgnoreCase) ||
                    origin.StartsWith("http://localhost:", StringComparison.OrdinalIgnoreCase) ||
                    origin.Equals("null", StringComparison.OrdinalIgnoreCase))
                    .AllowAnyHeader()
                    .AllowAnyMethod();
            });
        });

        builder.WebHost.ConfigureKestrel((context, options) => ConfigureKestrelHttps(context, options));

        var app = builder.Build();
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
            await DatabaseJsonNormalizer.NormalizeAsync(dbContext);
        }

        var skipDatabaseBackup = app.Configuration.GetValue<bool?>("SkipDatabaseBackup")
            ?? app.Configuration.GetValue<bool>("SKIP_DATABASE_BACKUP");

        if (skipDatabaseBackup)
        {
            var startupLogger = app.Services.GetRequiredService<ILogger<Program>>();
            startupLogger.LogInformation("Skipping startup database backup phase (SKIP_DATABASE_BACKUP is enabled).");
        }
        else
        {
            await app.Services.GetRequiredService<IDatabaseTools>().Backup();
        }
        await ConfigureQuartzTriggersAsync(app.Services);
        PenguinTwitchBot.Bot.Actions.SubActions.VariableReplacer.ServiceBackbone = app.Services.GetService<PenguinTwitchBot.Bot.Core.IServiceBackbone>();

        // Configure the HTTP request pipeline.
        if (!app.Environment.IsDevelopment())
        {
            app.UseExceptionHandler("/Error");
            // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.

        }

        app.UseStatusCodePagesWithReExecute("/NotFoundItem", "?statusCode={0}");

        app.UseStaticFiles(new StaticFileOptions
        {
            OnPrepareResponse = ctx =>
            {
                var path = ctx.Context.Request.Path.Value ?? string.Empty;
                if (path.StartsWith("/extension", StringComparison.OrdinalIgnoreCase) ||
                    path.EndsWith(".html", StringComparison.OrdinalIgnoreCase) ||
                    path.EndsWith(".js", StringComparison.OrdinalIgnoreCase) ||
                    path.EndsWith(".css", StringComparison.OrdinalIgnoreCase))
                {
                    ctx.Context.Response.Headers.Remove("X-Frame-Options");
                    ctx.Context.Response.Headers["Access-Control-Allow-Origin"] = "*";
                    ctx.Context.Response.Headers["Access-Control-Allow-Private-Network"] = "true";
                    ctx.Context.Response.Headers["Content-Security-Policy"] = "frame-ancestors 'self' https://*.twitch.tv https://twitch.tv https://*.ext-twitch.tv http://localhost:* https://localhost:*";
                }
            }
        });

        app.UseRouting();
        app.UseCors("TwitchExtensionCors");
        app.UseAuthentication();
        app.UseAuthorization();
        app.Use(async (context, next) =>
        {
            if (context.Request.Path.StartsWithSegments("/api/twitch-extension"))
            {
                context.Response.Headers.Append("Access-Control-Allow-Private-Network", "true");
            }
            await next();
        });

        var wsOptions = new WebSocketOptions
        {
            KeepAliveInterval = TimeSpan.FromMinutes(2)
        };
        app.UseWebSockets(wsOptions);
        app.MapControllerRoute(
            name: "default",
            pattern: "{controller=Home}/{action=Index}/{id?}");

        app.MapControllers();
        app.MapGet("/extension", () => Results.Redirect("/extension/panel.html")).AllowAnonymous();
        app.MapGet("/extension/mobile", () => Results.Redirect("/extension/mobile.html")).AllowAnonymous();
        app.MapGet("/extension/config", () => Results.Redirect("/extension/config.html")).AllowAnonymous();
        app.MapGet("/panel.html", () => Results.Redirect("/extension/panel.html")).AllowAnonymous();
        app.MapGet("/mobile.html", () => Results.Redirect("/extension/mobile.html")).AllowAnonymous();
        app.MapGet("/config.html", () => Results.Redirect("/extension/config.html")).AllowAnonymous();
        app.MapGet("/sitemap.xml", (HttpContext context) =>
        {
            string sitemapXml;
            try
            {
                var publicBaseUri = GetPublicBaseUriFromRequest(context.Request);
                sitemapXml = BuildSitemapXml(publicBaseUri);
            }
            catch (Exception ex)
            {
                app.Logger.LogWarning(ex, "Failed to generate sitemap.xml from request origin; returning empty sitemap.");
                sitemapXml = BuildEmptySitemapXml();
            }

            return Results.Content(sitemapXml, "application/xml; charset=utf-8");
        }).AllowAnonymous();

        app.MapGet("/robots.txt", (HttpContext context) =>
        {
            string robots;
            try
            {
                var publicBaseUri = GetPublicBaseUriFromRequest(context.Request);
                robots = $"User-agent: *\nAllow: /\nSitemap: {publicBaseUri}/sitemap.xml\n";
            }
            catch (Exception ex)
            {
                app.Logger.LogWarning(ex, "Failed to generate robots.txt sitemap URL from request origin; returning safe robots fallback.");
                robots = "User-agent: *\nAllow: /\n";
            }

            return Results.Text(robots, "text/plain");
        }).AllowAnonymous();

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
        lifetime.ApplicationStopping.Register(() =>
            {
                logger?.LogInformation("Application trying to stop.");
                // Fire-and-forget: don't block the shutdown thread. A blocked synchronous wait
                // ties up a thread-pool thread and can itself cause starvation during shutdown.
                _ = Task.Run(async () =>
                {
                    try
                    {
                        var closeTask = websocketMessenger.CloseAllSockets();
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
        catch (OperationCanceledException)
        {
            Log.Information("Host shutdown completed after cancellation request.");
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
}
