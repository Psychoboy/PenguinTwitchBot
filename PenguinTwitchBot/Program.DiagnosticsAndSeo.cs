using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components;
using Serilog;
using Serilog.Events;
using System.Reflection;
using System.Xml.Linq;

internal partial class Program
{
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


    private static string BuildSitemapXml(string origin)
    {
        var normalizedOrigin = origin.TrimEnd('/');
        var routes = GetPublicBlazorRoutes();
        XNamespace ns = "http://www.sitemaps.org/schemas/sitemap/0.9";

        var urlSet = new XElement(ns + "urlset",
            routes.Select(route =>
                new XElement(ns + "url",
                    new XElement(ns + "loc", $"{normalizedOrigin}{route}"))));

        var document = new XDocument(new XDeclaration("1.0", "utf-8", "yes"), urlSet);
        return document.ToString(SaveOptions.DisableFormatting);
    }

    private static string BuildEmptySitemapXml()
    {
        XNamespace ns = "http://www.sitemaps.org/schemas/sitemap/0.9";
        var urlSet = new XElement(ns + "urlset");
        var document = new XDocument(new XDeclaration("1.0", "utf-8", "yes"), urlSet);
        return document.ToString(SaveOptions.DisableFormatting);
    }

    private static IReadOnlyList<string> GetPublicBlazorRoutes()
    {
        var appAssembly = typeof(Program).Assembly;
        var routes = appAssembly.GetTypes()
            .Where(IsPublicRoutableComponent)
            .SelectMany(type => type.GetCustomAttributes<RouteAttribute>(inherit: true))
            .Select(attribute => NormalizeRoute(attribute.Template))
            .Where(static route => !string.IsNullOrWhiteSpace(route))
            .Where(static route => !route.Contains('{'))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(static route => route, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return routes;
    }

    private static bool IsPublicRoutableComponent(Type type)
    {
        if (!typeof(IComponent).IsAssignableFrom(type) || type.IsAbstract || type.IsGenericTypeDefinition)
        {
            return false;
        }

        if (!type.IsDefined(typeof(RouteAttribute), inherit: true))
        {
            return false;
        }

        if (type.Namespace?.Contains(".Pages.LegacyRoutes", StringComparison.Ordinal) == true)
        {
            return false;
        }

        var hasAuthorize = type.IsDefined(typeof(AuthorizeAttribute), inherit: true);
        var hasAllowAnonymous = type.IsDefined(typeof(AllowAnonymousAttribute), inherit: true);
        return !hasAuthorize || hasAllowAnonymous;
    }

    private static string NormalizeRoute(string template)
    {
        if (string.IsNullOrWhiteSpace(template))
        {
            return "/";
        }

        var route = template.StartsWith("/", StringComparison.Ordinal) ? template : $"/{template}";
        return route == string.Empty ? "/" : route;
    }

    private static Uri BuildOtlpHttpSignalEndpoint(string endpoint, string signal)
    {
        if (!Uri.TryCreate(endpoint, UriKind.Absolute, out var parsed))
        {
            throw new InvalidOperationException($"Invalid OpenTelemetry endpoint '{endpoint}'.");
        }

        var builder = new UriBuilder(parsed);
        var path = builder.Path.TrimEnd('/');
        var expectedSuffix = $"/v1/{signal}";

        if (!path.EndsWith(expectedSuffix, StringComparison.OrdinalIgnoreCase))
        {
            var normalizedRoot = path.Length == 0 ? string.Empty : path;
            if (normalizedRoot.EndsWith("/v1/logs", StringComparison.OrdinalIgnoreCase) ||
                normalizedRoot.EndsWith("/v1/traces", StringComparison.OrdinalIgnoreCase) ||
                normalizedRoot.EndsWith("/v1/metrics", StringComparison.OrdinalIgnoreCase))
            {
                normalizedRoot = normalizedRoot[..normalizedRoot.LastIndexOf("/v1/", StringComparison.OrdinalIgnoreCase)];
            }

            builder.Path = $"{normalizedRoot}{expectedSuffix}";
        }

        return builder.Uri;
    }

    private static string GetPublicBaseUriFromRequest(HttpRequest request)
    {
        if (!string.Equals(request.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(request.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
        {
            return "http://localhost";
        }

        if (!request.Host.HasValue)
        {
            return "http://localhost";
        }

        var pathBase = request.PathBase.HasValue
            ? request.PathBase.Value?.TrimEnd('/') ?? string.Empty
            : string.Empty;
        var candidate = $"{request.Scheme}://{request.Host.Value}{pathBase}";

        if (!Uri.TryCreate(candidate, UriKind.Absolute, out var parsed))
        {
            return "http://localhost";
        }

        return parsed.AbsoluteUri.TrimEnd('/');
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
}
