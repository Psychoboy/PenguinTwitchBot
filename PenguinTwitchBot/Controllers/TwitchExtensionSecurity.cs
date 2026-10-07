using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.IdentityModel.Tokens;
using PenguinTwitchBot.Bot.Commands.Features;
using PenguinTwitchBot.Bot.TwitchServices;
using PenguinTwitchBot.Database.Bot.Models;

namespace PenguinTwitchBot.Controllers;

public sealed record TwitchExtensionClaims(
    string ChannelId,
    string? UserId,
    string OpaqueUserId,
    string Role,
    bool IsIdentityShared)
{
    public bool HasUserId => !string.IsNullOrWhiteSpace(UserId);
}

public static class TwitchExtensionSecurity
{
    private static readonly JwtSecurityTokenHandler TokenHandler = new();

    public static TwitchExtensionClaims? ParseAndValidateToken(
        string? authorizationHeader,
        IConfiguration configuration,
        ILogger logger)
    {
        var token = ExtractBearerToken(authorizationHeader);
        if (token == null)
        {
            return null;
        }

        var secret = GetExtensionSecret(configuration);
        var principal = ValidateOrReadToken(token, secret, logger);
        if (principal == null)
        {
            return null;
        }

        return ExtractClaims(principal);
    }

    private static string? ExtractBearerToken(string? authorizationHeader)
    {
        if (string.IsNullOrWhiteSpace(authorizationHeader))
        {
            return null;
        }

        var token = authorizationHeader.Trim();
        if (token.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            token = token["Bearer ".Length..].Trim();
        }

        return string.IsNullOrWhiteSpace(token) ? null : token;
    }

    private static string? GetExtensionSecret(IConfiguration configuration)
    {
        return configuration["TwitchExtension:Secret"]
               ?? configuration["Twitch:Extension:Secret"]
               ?? configuration["twitchExtensionSecret"]
               ?? configuration["Twitch:ExtensionSecret"];
    }

    private static ClaimsPrincipal? ValidateOrReadToken(string token, string? secret, ILogger logger)
    {
        if (string.IsNullOrWhiteSpace(secret))
        {
            logger.LogError("Twitch Extension secret is not configured; cannot validate JWT token");
            return null;
        }

        return ValidateSignedToken(token, secret, logger);
    }

    private static ClaimsPrincipal? ValidateSignedToken(string token, string secret, ILogger logger)
    {
        try
        {
            var keyBytes = Convert.FromBase64String(secret);
            var validationParameters = new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(keyBytes),
                ValidateIssuer = false,
                ValidateAudience = false,
                ValidateLifetime = true,
                ClockSkew = TimeSpan.FromMinutes(2)
            };

            return TokenHandler.ValidateToken(token, validationParameters, out _);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to validate Twitch Extension JWT signature");
            return null;
        }
    }

    private static TwitchExtensionClaims ExtractClaims(ClaimsPrincipal principal)
    {
        var channelId = principal.FindFirst("channel_id")?.Value
                        ?? principal.FindFirst("channelId")?.Value
                        ?? string.Empty;

        var userId = principal.FindFirst("user_id")?.Value
                     ?? principal.FindFirst("userId")?.Value;

        var opaqueUserId = principal.FindFirst("opaque_user_id")?.Value
                           ?? principal.FindFirst("opaqueUserId")?.Value
                           ?? string.Empty;

        var role = principal.FindFirst("role")?.Value ?? "viewer";

        // In Twitch extensions, if identity is not shared, user_id is either absent or matches opaque_user_id starting with 'A'
        var isShared = !string.IsNullOrWhiteSpace(userId) && !userId.StartsWith('A');

        return new TwitchExtensionClaims(
            ChannelId: channelId,
            UserId: isShared ? userId : null,
            OpaqueUserId: opaqueUserId,
            Role: role,
            IsIdentityShared: isShared);
    }

    public static async Task<Viewer?> ResolveViewerAsync(
        string userId,
        IViewerFeature viewerFeature,
        ITwitchService twitchService,
        ILogger logger)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            return null;
        }

        try
        {
            var existingViewer = await viewerFeature.GetViewerByUserId(userId);
            if (existingViewer != null)
            {
                return existingViewer;
            }

            var twitchUser = await twitchService.GetUserById(userId);
            if (twitchUser != null)
            {
                var newViewer = new Viewer
                {
                    UserId = twitchUser.Id,
                    Username = twitchUser.Login.ToLowerInvariant(),
                    DisplayName = twitchUser.DisplayName,
                    LastSeen = DateTime.UtcNow
                };

                await viewerFeature.SaveViewer(newViewer);
                return newViewer;
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Error resolving viewer for Twitch Extension user ID {UserId}", userId);
        }

        return null;
    }
}

