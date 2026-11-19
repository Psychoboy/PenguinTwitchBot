using System.Collections.Concurrent;
using System.Globalization;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PenguinTwitchBot.Bot.Actions;
using PenguinTwitchBot.Bot.Notifications;
using PenguinTwitchBot.Database.Bot.Core;
using PenguinTwitchBot.Database.Bot.Models;
using PenguinTwitchBot.Database.Bot.Models.Actions.Triggers;
using PenguinTwitchBot.Database.Bot.Models.Credits;
using PenguinTwitchBot.Database.Repository;

namespace PenguinTwitchBot.Bot.Overlay.Credits
{
    public sealed class CreditsService(
        IServiceScopeFactory scopeFactory,
        CreditsDataBuilder dataBuilder,
        ICreditsEventCollector eventCollector,
        IWebSocketMessenger webSocketMessenger,
        ILogger<CreditsService> logger,
        TimeProvider? timeProvider = null) : ICreditsService
    {
        private readonly SemaphoreSlim _seedLock = new(1, 1);
        private readonly SemaphoreSlim _profileLock = new(1, 1);
        private readonly TimeProvider _timeProvider = timeProvider ?? TimeProvider.System;
        private volatile bool _seeded;

        public async Task EnsureSeededAsync(CancellationToken cancellationToken = default)
        {
            if (_seeded) return;

            await _seedLock.WaitAsync(cancellationToken);
            try
            {
                if (_seeded) return;

                await using var scope = scopeFactory.CreateAsyncScope();
                var db = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

                var existingThemes = (await db.CreditThemes.GetAllAsync()).ToList();
                var byKey = existingThemes
                    .Where(t => !string.IsNullOrWhiteSpace(t.BuiltInKey))
                    .ToDictionary(t => t.BuiltInKey!, StringComparer.OrdinalIgnoreCase);
                var existingThemeNames = existingThemes
                    .Select(t => t.Name)
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);

                CreditTheme? defaultTheme = byKey.GetValueOrDefault(CreditsBuiltInThemes.DefaultThemeKey);
                var addedTheme = false;
                var now = _timeProvider.GetUtcNow().UtcDateTime;

                foreach (var preset in CreditsBuiltInThemes.All)
                {
                    if (byKey.ContainsKey(preset.BuiltInKey)) continue;

                    var name = existingThemeNames.Contains(preset.Name)
                        ? $"{preset.Name} (Built-in)"
                        : preset.Name;

                    var theme = new CreditTheme
                    {
                        Name = name,
                        Description = preset.Description,
                        IsBuiltIn = true,
                        BuiltInKey = preset.BuiltInKey,
                        ThemeJson = JsonSerializer.Serialize(preset.Config, CreditsDataBuilder.JsonOptions),
                        CreatedAtUtc = now,
                        UpdatedAtUtc = now
                    };
                    await db.CreditThemes.AddAsync(theme);
                    existingThemeNames.Add(name);
                    addedTheme = true;

                    if (preset.BuiltInKey == CreditsBuiltInThemes.DefaultThemeKey)
                        defaultTheme = theme;
                }

                if (addedTheme)
                    await db.SaveChangesAsync();

                defaultTheme ??= existingThemes.FirstOrDefault();

                var hasAnyProfile = await db.CreditProfiles.Query().AnyAsync(cancellationToken);
                if (!hasAnyProfile)
                {
                    var defaultConfig = CreditsBuiltInThemes.CreateDefaultProfileConfig();
                    var profile = new CreditProfile
                    {
                        Name = "Default",
                        IsDefault = true,
                        ThemeId = defaultTheme?.Id,
                        ConfigJson = JsonSerializer.Serialize(defaultConfig, CreditsDataBuilder.JsonOptions),
                        CreatedAtUtc = now,
                        UpdatedAtUtc = now
                    };
                    await db.CreditProfiles.AddAsync(profile);
                    await db.SaveChangesAsync();
                }

                _seeded = true;
            }
            finally
            {
                _seedLock.Release();
            }
        }

        public async Task<List<CreditProfile>> GetProfilesAsync(CancellationToken cancellationToken = default)
        {
            await EnsureSeededAsync(cancellationToken);
            await using var scope = scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            return await db.CreditProfiles
                .Query()
                .Include(p => p.Theme)
                .OrderByDescending(p => p.IsDefault)
                .ThenBy(p => p.Name)
                .ToListAsync(cancellationToken);
        }

        public async Task<CreditProfile?> GetProfileByIdAsync(Guid id, CancellationToken cancellationToken = default)
        {
            await EnsureSeededAsync(cancellationToken);
            await using var scope = scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            return await db.CreditProfiles
                .Find(p => p.Id == id)
                .Include(p => p.Theme)
                .FirstOrDefaultAsync(cancellationToken);
        }

        public async Task<CreditProfile> ResolveProfileAsync(string? profileName = null, CancellationToken cancellationToken = default)
        {
            await EnsureSeededAsync(cancellationToken);
            await using var scope = scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

            var all = await db.CreditProfiles
                .Query()
                .Include(p => p.Theme)
                .ToListAsync(cancellationToken);

            if (!string.IsNullOrWhiteSpace(profileName))
            {
                var trimmed = profileName.Trim();
                var exact = all.FirstOrDefault(p => p.Name.Equals(trimmed, StringComparison.OrdinalIgnoreCase));
                if (exact != null) return exact;
            }

            return all.FirstOrDefault(p => p.IsDefault)
                ?? all.FirstOrDefault()
                ?? throw new InvalidOperationException("No credits profile exists.");
        }

        public async Task<IReadOnlyList<string>> GetProfileNamesAsync(CancellationToken cancellationToken = default)
        {
            var profiles = await GetProfilesAsync(cancellationToken);
            return profiles.Select(p => p.Name).ToList();
        }

        public Task<CreditProfile> CreateProfileAsync(
            string name,
            Guid? themeId = null,
            CreditsProfileConfig? config = null,
            bool isDefault = false,
            CancellationToken cancellationToken = default) =>
            CreateProfileInternalAsync(name, themeId, config, isDefault, assignDefaultThemeIfNull: true, cancellationToken);

        private async Task<CreditProfile> CreateProfileInternalAsync(
            string name,
            Guid? themeId,
            CreditsProfileConfig? config,
            bool isDefault,
            bool assignDefaultThemeIfNull,
            CancellationToken cancellationToken)
        {
            await EnsureSeededAsync(cancellationToken);
            var trimmedName = string.IsNullOrWhiteSpace(name)
                ? throw new ArgumentException("Profile name is required.", nameof(name))
                : name.Trim();

            var normalizedConfig = config ?? CreditsBuiltInThemes.CreateDefaultProfileConfig();
            CreditsDataBuilder.NormalizeProfileConfig(normalizedConfig);

            Guid createdId;
            await _profileLock.WaitAsync(cancellationToken);
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var db = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

                var existingNames = await db.CreditProfiles
                    .Query()
                    .Select(p => p.Name)
                    .ToListAsync(cancellationToken);
                var exists = existingNames.Any(n => n.Equals(trimmedName, StringComparison.OrdinalIgnoreCase));
                if (exists)
                    throw new InvalidOperationException($"A credits profile named '{trimmedName}' already exists.");

                if (!themeId.HasValue && assignDefaultThemeIfNull)
                {
                    var defaultTheme = await db.CreditThemes
                        .Find(t => t.BuiltInKey == CreditsBuiltInThemes.DefaultThemeKey)
                        .FirstOrDefaultAsync(cancellationToken);
                    themeId = defaultTheme?.Id;
                }

                var now = _timeProvider.GetUtcNow().UtcDateTime;
                if (isDefault)
                {
                    var currentDefaults = await db.CreditProfiles.Find(p => p.IsDefault).ToListAsync(cancellationToken);
                    foreach (var d in currentDefaults)
                    {
                        d.IsDefault = false;
                        d.UpdatedAtUtc = now;
                        db.CreditProfiles.Update(d);
                    }
                }

                var profile = new CreditProfile
                {
                    Name = trimmedName,
                    IsDefault = isDefault,
                    ThemeId = themeId,
                    ConfigJson = JsonSerializer.Serialize(normalizedConfig, CreditsDataBuilder.JsonOptions),
                    CreatedAtUtc = now,
                    UpdatedAtUtc = now
                };

                await db.CreditProfiles.AddAsync(profile);
                await db.SaveChangesAsync();
                createdId = profile.Id;
            }
            finally
            {
                _profileLock.Release();
            }

            return (await GetProfileByIdAsync(createdId, cancellationToken))!;
        }

        public async Task<CreditProfile> SaveProfileAsync(
            Guid id,
            string name,
            Guid? themeId,
            CreditsProfileConfig config,
            bool isDefault,
            CancellationToken cancellationToken = default)
        {
            await EnsureSeededAsync(cancellationToken);
            var trimmedName = string.IsNullOrWhiteSpace(name)
                ? throw new ArgumentException("Profile name is required.", nameof(name))
                : name.Trim();

            CreditsDataBuilder.NormalizeProfileConfig(config);

            await _profileLock.WaitAsync(cancellationToken);
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var db = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

                var existing = await db.CreditProfiles.GetByIdAsync(id)
                    ?? throw new KeyNotFoundException($"Credits profile {id} was not found.");

                var profileEntries = await db.CreditProfiles
                    .Query()
                    .Select(p => new { p.Id, p.Name })
                    .ToListAsync(cancellationToken);
                var nameConflict = profileEntries.Any(p => p.Id != id && p.Name.Equals(trimmedName, StringComparison.OrdinalIgnoreCase));
                if (nameConflict)
                    throw new InvalidOperationException($"Another credits profile named '{trimmedName}' already exists.");

                var now = _timeProvider.GetUtcNow().UtcDateTime;

                if (isDefault && !existing.IsDefault)
                {
                    var currentDefaults = await db.CreditProfiles.Find(p => p.Id != id && p.IsDefault).ToListAsync(cancellationToken);
                    foreach (var d in currentDefaults)
                    {
                        d.IsDefault = false;
                        d.UpdatedAtUtc = now;
                        db.CreditProfiles.Update(d);
                    }
                }

                existing.Name = trimmedName;
                existing.ThemeId = themeId;
                existing.IsDefault = isDefault || existing.IsDefault; // Can't unset default without making another default
                existing.ConfigJson = JsonSerializer.Serialize(config, CreditsDataBuilder.JsonOptions);
                existing.UpdatedAtUtc = now;
                db.CreditProfiles.Update(existing);
                await db.SaveChangesAsync();
            }
            finally
            {
                _profileLock.Release();
            }

            return (await GetProfileByIdAsync(id, cancellationToken))!;
        }

        public async Task<CreditProfile> CloneProfileAsync(Guid sourceId, string newName, CancellationToken cancellationToken = default)
        {
            var source = await GetProfileByIdAsync(sourceId, cancellationToken)
                ?? throw new KeyNotFoundException($"Credits profile {sourceId} was not found.");
            var config = CreditsDataBuilder.DeserializeProfileConfig(source.ConfigJson);
            return await CreateProfileInternalAsync(newName, source.ThemeId, config, isDefault: false, assignDefaultThemeIfNull: false, cancellationToken);
        }

        public async Task SetDefaultProfileAsync(Guid id, CancellationToken cancellationToken = default)
        {
            await EnsureSeededAsync(cancellationToken);
            await _profileLock.WaitAsync(cancellationToken);
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var db = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
                var all = (await db.CreditProfiles.GetAllAsync()).ToList();
                if (all.All(p => p.Id != id))
                    throw new KeyNotFoundException($"Credits profile {id} was not found.");

                var now = _timeProvider.GetUtcNow().UtcDateTime;
                foreach (var p in all)
                {
                    p.IsDefault = p.Id == id;
                    p.UpdatedAtUtc = now;
                    db.CreditProfiles.Update(p);
                }
                await db.SaveChangesAsync();
            }
            finally
            {
                _profileLock.Release();
            }
        }

        public async Task<bool> DeleteProfileAsync(Guid id, CancellationToken cancellationToken = default)
        {
            await EnsureSeededAsync(cancellationToken);
            await _profileLock.WaitAsync(cancellationToken);
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var db = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
                var all = (await db.CreditProfiles.GetAllAsync()).ToList();
                if (all.Count <= 1) return false;

                var target = all.FirstOrDefault(p => p.Id == id);
                if (target == null) return false;

                var wasDefault = target.IsDefault;
                db.CreditProfiles.Remove(target);

                if (wasDefault)
                {
                    var nextDefault = all.First(p => p.Id != id);
                    nextDefault.IsDefault = true;
                    db.CreditProfiles.Update(nextDefault);
                }

                await db.SaveChangesAsync();
                return true;
            }
            finally
            {
                _profileLock.Release();
            }
        }

        public async Task<List<CreditTheme>> GetThemesAsync(CancellationToken cancellationToken = default)
        {
            await EnsureSeededAsync(cancellationToken);
            await using var scope = scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            return await db.CreditThemes
                .Query()
                .OrderByDescending(t => t.IsBuiltIn)
                .ThenBy(t => t.Name)
                .ToListAsync(cancellationToken);
        }

        public async Task<CreditTheme?> GetThemeByIdAsync(Guid id, CancellationToken cancellationToken = default)
        {
            await EnsureSeededAsync(cancellationToken);
            await using var scope = scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            return await db.CreditThemes.GetByIdAsync(id);
        }

        public async Task<CreditTheme> CreateThemeAsync(
            string name,
            string? description,
            CreditsThemeConfig config,
            CancellationToken cancellationToken = default)
        {
            await EnsureSeededAsync(cancellationToken);
            var trimmedName = string.IsNullOrWhiteSpace(name)
                ? throw new ArgumentException("Theme name is required.", nameof(name))
                : name.Trim();

            await using var scope = scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

            var themeNames = await db.CreditThemes
                .Query()
                .Select(t => t.Name)
                .ToListAsync(cancellationToken);
            var exists = themeNames.Any(n => n.Equals(trimmedName, StringComparison.OrdinalIgnoreCase));
            if (exists)
                throw new InvalidOperationException($"A credits theme named '{trimmedName}' already exists.");

            var now = _timeProvider.GetUtcNow().UtcDateTime;
            var theme = new CreditTheme
            {
                Name = trimmedName,
                Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim(),
                IsBuiltIn = false,
                BuiltInKey = null,
                ThemeJson = JsonSerializer.Serialize(config, CreditsDataBuilder.JsonOptions),
                CreatedAtUtc = now,
                UpdatedAtUtc = now
            };

            await db.CreditThemes.AddAsync(theme);
            await db.SaveChangesAsync();
            return theme;
        }

        public async Task<CreditTheme> SaveThemeAsync(
            Guid id,
            string name,
            string? description,
            CreditsThemeConfig config,
            CancellationToken cancellationToken = default)
        {
            await EnsureSeededAsync(cancellationToken);
            var trimmedName = string.IsNullOrWhiteSpace(name)
                ? throw new ArgumentException("Theme name is required.", nameof(name))
                : name.Trim();

            await using var scope = scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            var theme = await db.CreditThemes.GetByIdAsync(id)
                ?? throw new KeyNotFoundException($"Credits theme {id} was not found.");

            var themeEntries = await db.CreditThemes
                .Query()
                .Select(t => new { t.Id, t.Name })
                .ToListAsync(cancellationToken);
            var conflict = themeEntries.Any(t => t.Id != id && t.Name.Equals(trimmedName, StringComparison.OrdinalIgnoreCase));
            if (conflict)
                throw new InvalidOperationException($"Another credits theme named '{trimmedName}' already exists.");

            if (!theme.IsBuiltIn)
            {
                theme.Name = trimmedName;
            }
            theme.Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
            theme.ThemeJson = JsonSerializer.Serialize(config, CreditsDataBuilder.JsonOptions);
            theme.UpdatedAtUtc = _timeProvider.GetUtcNow().UtcDateTime;

            db.CreditThemes.Update(theme);
            await db.SaveChangesAsync();
            return theme;
        }

        public async Task<CreditTheme> CloneThemeAsync(Guid sourceId, string newName, CancellationToken cancellationToken = default)
        {
            var source = await GetThemeByIdAsync(sourceId, cancellationToken)
                ?? throw new KeyNotFoundException($"Credits theme {sourceId} was not found.");
            var config = CreditsDataBuilder.DeserializeThemeConfig(source.ThemeJson);
            return await CreateThemeAsync(newName, source.Description, config, cancellationToken);
        }

        public async Task<CreditTheme?> ResetBuiltInThemeAsync(Guid id, CancellationToken cancellationToken = default)
        {
            await EnsureSeededAsync(cancellationToken);
            await using var scope = scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            var theme = await db.CreditThemes.GetByIdAsync(id);
            if (theme == null || !theme.IsBuiltIn || string.IsNullOrWhiteSpace(theme.BuiltInKey))
                return null;

            var def = CreditsBuiltInThemes.All.FirstOrDefault(x =>
                x.BuiltInKey.Equals(theme.BuiltInKey, StringComparison.OrdinalIgnoreCase));
            if (def == null) return null;

            theme.Description = def.Description;
            theme.ThemeJson = JsonSerializer.Serialize(def.Config, CreditsDataBuilder.JsonOptions);
            theme.UpdatedAtUtc = _timeProvider.GetUtcNow().UtcDateTime;
            db.CreditThemes.Update(theme);
            await db.SaveChangesAsync();
            return theme;
        }

        public async Task<bool> DeleteThemeAsync(Guid id, CancellationToken cancellationToken = default)
        {
            await EnsureSeededAsync(cancellationToken);
            await using var scope = scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            var theme = await db.CreditThemes.GetByIdAsync(id);
            if (theme == null || theme.IsBuiltIn) return false;

            db.CreditThemes.Remove(theme);
            await db.SaveChangesAsync();
            return true;
        }

        public async Task<string?> ExportProfileJsonAsync(Guid id, CancellationToken cancellationToken = default)
        {
            var profile = await GetProfileByIdAsync(id, cancellationToken);
            if (profile == null) return null;

            var export = new CreditsProfileExportDocument
            {
                Name = profile.Name,
                ThemeName = profile.Theme?.Name,
                ThemeBuiltInKey = profile.Theme?.BuiltInKey,
                Config = CreditsDataBuilder.DeserializeProfileConfig(profile.ConfigJson)
            };
            return JsonSerializer.Serialize(export, CreditsDataBuilder.JsonOptions);
        }

        public async Task<CreditProfile> ImportProfileAsync(string json, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(json))
                throw new ArgumentException("Import JSON cannot be empty.", nameof(json));

            var doc = JsonSerializer.Deserialize<CreditsProfileExportDocument>(json, CreditsDataBuilder.JsonOptions)
                ?? throw new InvalidOperationException("Invalid credits profile JSON.");
            if (doc.Config == null)
                throw new InvalidOperationException("Profile configuration is missing.");

            var existingProfiles = await GetProfilesAsync(cancellationToken);
            var existingThemes = await GetThemesAsync(cancellationToken);

            var uniqueName = MakeUniqueName(
                string.IsNullOrWhiteSpace(doc.Name) ? "Imported Profile" : doc.Name,
                existingProfiles.Select(p => p.Name));

            Guid? matchedThemeId = null;
            if (!string.IsNullOrWhiteSpace(doc.ThemeBuiltInKey))
            {
                matchedThemeId = existingThemes.FirstOrDefault(t =>
                    string.Equals(t.BuiltInKey, doc.ThemeBuiltInKey, StringComparison.OrdinalIgnoreCase))?.Id;
            }
            if (!matchedThemeId.HasValue && !string.IsNullOrWhiteSpace(doc.ThemeName))
            {
                matchedThemeId = existingThemes.FirstOrDefault(t =>
                    t.Name.Equals(doc.ThemeName, StringComparison.OrdinalIgnoreCase))?.Id;
            }

            return await CreateProfileAsync(uniqueName, matchedThemeId, doc.Config, isDefault: false, cancellationToken);
        }

        public async Task<string?> ExportThemeJsonAsync(Guid id, CancellationToken cancellationToken = default)
        {
            var theme = await GetThemeByIdAsync(id, cancellationToken);
            if (theme == null) return null;

            var export = new CreditsThemeExportDocument
            {
                Name = theme.Name,
                Description = theme.Description,
                Config = CreditsDataBuilder.DeserializeThemeConfig(theme.ThemeJson)
            };
            return JsonSerializer.Serialize(export, CreditsDataBuilder.JsonOptions);
        }

        public async Task<CreditTheme> ImportThemeAsync(string json, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(json))
                throw new ArgumentException("Import JSON cannot be empty.", nameof(json));

            var doc = JsonSerializer.Deserialize<CreditsThemeExportDocument>(json, CreditsDataBuilder.JsonOptions)
                ?? throw new InvalidOperationException("Invalid credits theme JSON.");
            if (doc.Config == null)
                throw new InvalidOperationException("Theme configuration is missing.");

            var existingThemes = await GetThemesAsync(cancellationToken);
            var uniqueName = MakeUniqueName(
                string.IsNullOrWhiteSpace(doc.Name) ? "Imported Theme" : doc.Name,
                existingThemes.Select(t => t.Name));

            return await CreateThemeAsync(uniqueName, doc.Description, doc.Config, cancellationToken);
        }

        public async Task<CreditsRenderPayload> RenderAsync(
            string? profileName = null,
            string? sampleMode = null,
            string? windowOverrideMode = null,
            int? lookbackMinutes = null,
            CancellationToken cancellationToken = default)
        {
            var profile = await ResolveProfileAsync(profileName, cancellationToken);
            return await dataBuilder.BuildPayloadAsync(
                profile,
                profile.Theme,
                sampleMode,
                windowOverrideMode,
                lookbackMinutes,
                cancellationToken);
        }

        public async Task PlayAsync(
            string? profileName = null,
            string? windowOverrideMode = null,
            int? lookbackMinutes = null,
            string? sampleMode = null,
            CancellationToken cancellationToken = default)
        {
            var payload = await RenderAsync(profileName, sampleMode, windowOverrideMode, lookbackMinutes, cancellationToken);
            await BroadcastJsonAsync(new { type = "credits_play", profile = payload.ProfileName, payload });
        }

        public async Task StopAsync(string? profileName = null) =>
            await BroadcastJsonAsync(new { type = "credits_stop", profile = await ResolveTargetProfileNameAsync(profileName) });

        public async Task PauseAsync(string? profileName = null) =>
            await BroadcastJsonAsync(new { type = "credits_pause", profile = await ResolveTargetProfileNameAsync(profileName) });

        public async Task ResumeAsync(string? profileName = null) =>
            await BroadcastJsonAsync(new { type = "credits_resume", profile = await ResolveTargetProfileNameAsync(profileName) });

        public async Task TogglePauseAsync(string? profileName = null) =>
            await BroadcastJsonAsync(new { type = "credits_toggle_pause", profile = await ResolveTargetProfileNameAsync(profileName) });

        public async Task RestartAsync(string? profileName = null) =>
            await BroadcastJsonAsync(new { type = "credits_restart", profile = await ResolveTargetProfileNameAsync(profileName) });

        public async Task SetSpeedAsync(string? profileName, double multiplier) =>
            await BroadcastJsonAsync(new { type = "credits_speed", profile = await ResolveTargetProfileNameAsync(profileName), speed = Math.Clamp(multiplier, 0.25, 10.0) });

        public Task NotifyReloadAsync(string? profileName = null) =>
            BroadcastJsonAsync(new { type = "credits_reload", profile = string.IsNullOrWhiteSpace(profileName) ? "*" : profileName.Trim() });

        public async Task<List<CreditStreamSession>> GetSessionsAsync(int limit = 50, CancellationToken cancellationToken = default)
        {
            await EnsureSeededAsync(cancellationToken);
            await using var scope = scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            return await db.CreditStreamSessions
                .Query()
                .OrderByDescending(s => s.StartedAtUtc)
                .Take(Math.Clamp(limit, 1, 200))
                .ToListAsync(cancellationToken);
        }

        public async Task<DateTime> MarkManualCheckpointAsync(
            string? label = null,
            string? profileName = null,
            CancellationToken cancellationToken = default)
        {
            await EnsureSeededAsync(cancellationToken);
            var checkpointUtc = _timeProvider.GetUtcNow().UtcDateTime;

            if (string.IsNullOrWhiteSpace(profileName))
            {
                await eventCollector.MarkManualCheckpointAsync(label, checkpointUtc, cancellationToken);
            }

            await _profileLock.WaitAsync(cancellationToken);
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var db = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

                var allProfiles = (await db.CreditProfiles.GetAllAsync()).ToList();
                List<CreditProfile> targets = !string.IsNullOrWhiteSpace(profileName)
                    ? allProfiles.Where(p => p.Name.Equals(profileName.Trim(), StringComparison.OrdinalIgnoreCase)).ToList()
                    : allProfiles;

                foreach (var p in targets)
                {
                    var cfg = CreditsDataBuilder.DeserializeProfileConfig(p.ConfigJson);
                    if (string.IsNullOrWhiteSpace(profileName) &&
                        cfg.TimeWindow.Mode != CreditTimeWindowModes.SinceManualCheckpoint)
                    {
                        continue;
                    }

                    cfg.TimeWindow.ManualCheckpointUtc = checkpointUtc;
                    p.ConfigJson = JsonSerializer.Serialize(cfg, CreditsDataBuilder.JsonOptions);
                    p.UpdatedAtUtc = checkpointUtc;
                    db.CreditProfiles.Update(p);
                }

                await db.SaveChangesAsync();
            }
            finally
            {
                _profileLock.Release();
            }

            return checkpointUtc;
        }

        public async Task<List<CreditEvent>> GetEventsAsync(
            DateTime startUtc,
            DateTime endUtc,
            string? eventType = null,
            bool includeHidden = true,
            CancellationToken cancellationToken = default)
        {
            await EnsureSeededAsync(cancellationToken);
            await eventCollector.FlushPendingChattersAsync(cancellationToken);

            await using var scope = scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            var query = db.CreditEvents.Find(e => e.OccurredAtUtc >= startUtc && e.OccurredAtUtc <= endUtc);

            if (!includeHidden)
                query = query.Where(e => !e.IsHidden);

            if (!string.IsNullOrWhiteSpace(eventType))
                query = query.Where(e => e.EventType == eventType);

            return await query
                .OrderByDescending(e => e.OccurredAtUtc)
                .Take(1000)
                .ToListAsync(cancellationToken);
        }

        public async Task<CreditEvent> AddManualEventAsync(
            string username,
            string? displayName,
            decimal amount,
            string? detail,
            string? avatarUrl = null,
            string? eventType = null,
            CancellationToken cancellationToken = default)
        {
            await EnsureSeededAsync(cancellationToken);
            var resolvedType = string.IsNullOrWhiteSpace(eventType) ? CreditEventTypes.Manual : eventType.Trim();
            var normUsername = UsernameNormalizer.Normalize(username);
            var safeDisplay = string.IsNullOrWhiteSpace(displayName) ? username.Trim() : displayName.Trim();

            var entity = new CreditEvent
            {
                EventType = resolvedType,
                Username = normUsername,
                DisplayName = safeDisplay,
                Amount = amount,
                Detail = string.IsNullOrWhiteSpace(detail) ? null : detail.Trim(),
                AvatarUrl = string.IsNullOrWhiteSpace(avatarUrl) ? null : avatarUrl.Trim(),
                OccurredAtUtc = _timeProvider.GetUtcNow().UtcDateTime,
                StreamSessionId = eventCollector.CurrentStreamSessionId,
                IsManual = true
            };

            await using var scope = scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            await db.CreditEvents.AddAsync(entity);
            await db.SaveChangesAsync();
            return entity;
        }

        public async Task<bool> SetEventHiddenAsync(Guid eventId, bool isHidden, CancellationToken cancellationToken = default)
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            var ev = await db.CreditEvents.GetByIdAsync(eventId);
            if (ev == null) return false;

            ev.IsHidden = isHidden;
            db.CreditEvents.Update(ev);
            await db.SaveChangesAsync();
            return true;
        }

        public async Task<bool> DeleteEventAsync(Guid eventId, CancellationToken cancellationToken = default)
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            var ev = await db.CreditEvents.GetByIdAsync(eventId);
            if (ev == null) return false;

            db.CreditEvents.Remove(ev);
            await db.SaveChangesAsync();
            return true;
        }

        public async Task<int> GetRetentionDaysAsync(CancellationToken cancellationToken = default)
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            var setting = await db.Settings.Find(s => s.Name == CreditsEventCollectorService.RetentionSettingName).FirstOrDefaultAsync(cancellationToken);
            return setting?.IntSetting ?? CreditsEventCollectorService.DefaultRetentionDays;
        }

        public async Task SetRetentionDaysAsync(int days, CancellationToken cancellationToken = default)
        {
            var clamped = Math.Max(0, days);
            await using var scope = scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            var setting = await db.Settings.Find(s => s.Name == CreditsEventCollectorService.RetentionSettingName).FirstOrDefaultAsync(cancellationToken);
            if (setting == null)
            {
                await db.Settings.AddAsync(new Setting
                {
                    Name = CreditsEventCollectorService.RetentionSettingName,
                    DataType = Setting.DataTypeEnum.Int,
                    IntSetting = clamped
                });
            }
            else
            {
                setting.DataType = Setting.DataTypeEnum.Int;
                setting.IntSetting = clamped;
                db.Settings.Update(setting);
            }
            await db.SaveChangesAsync();
        }

        public async Task TriggerCompletedAsync(
            string profileName,
            int totalEntries,
            int totalSections,
            CancellationToken cancellationToken = default)
        {
            try
            {
                var safeProfile = string.IsNullOrWhiteSpace(profileName) ? "Default" : profileName.Trim();
                await using var scope = scopeFactory.CreateAsyncScope();
                var actionService = scope.ServiceProvider.GetRequiredService<IActionManagementService>();
                var actionRunner = scope.ServiceProvider.GetRequiredService<IAction>();

                var specificActions = await actionService.GetActionsByTriggerTypeAndNameEnabledAsync(
                    TriggerTypes.CreditsCompleted,
                    ICreditsService.CompletedTriggerNamePrefix + safeProfile);

                var wildcardActions = await actionService.GetActionsByTriggerTypeAndNameEnabledAsync(
                    TriggerTypes.CreditsCompleted,
                    ICreditsService.CompletedTriggerNamePrefix + ICreditsService.AnyProfileTriggerKey);

                var combined = specificActions
                    .Concat(wildcardActions)
                    .GroupBy(a => a.Id)
                    .Select(g => g.First())
                    .ToList();

                foreach (var actionItem in combined)
                {
                    var variables = new ConcurrentDictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                    {
                        ["creditsProfile"] = safeProfile,
                        ["totalEntries"] = totalEntries.ToString(CultureInfo.InvariantCulture),
                        ["totalSections"] = totalSections.ToString(CultureInfo.InvariantCulture)
                    };
                    await actionRunner.EnqueueAction(variables, actionItem);
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed triggering CreditsCompleted actions for profile {Profile}.", SanitizeForLog(profileName));
                throw;
            }
        }

        internal static string SanitizeForLog(string? input)
        {
            if (string.IsNullOrEmpty(input)) return string.Empty;
            var sanitized = input.Replace(Environment.NewLine, string.Empty).Replace("\r", string.Empty).Replace("\n", string.Empty);
            return sanitized.Length > 120 ? sanitized[..120] : sanitized;
        }

        private Task BroadcastJsonAsync(object message)
        {
            var json = JsonSerializer.Serialize(message, CreditsDataBuilder.JsonOptions);
            return webSocketMessenger.AddToQueue(WsTopics.Credits, json);
        }

        private async Task<string> ResolveTargetProfileNameAsync(string? profileName)
        {
            if (!string.IsNullOrWhiteSpace(profileName))
            {
                return profileName.Trim();
            }

            var resolvedDefault = await ResolveProfileAsync(null);
            return resolvedDefault.Name;
        }

        private static string MakeUniqueName(string baseName, IEnumerable<string> existingNames)
        {
            var set = existingNames.ToHashSet(StringComparer.OrdinalIgnoreCase);
            var trimmed = baseName.Trim();
            if (!set.Contains(trimmed)) return trimmed;

            for (var i = 2; i < 1000; i++)
            {
                var candidate = $"{trimmed} ({i})";
                if (!set.Contains(candidate)) return candidate;
            }
            return $"{trimmed} ({Guid.NewGuid().ToString("N")[..6]})";
        }

        private sealed class CreditsProfileExportDocument
        {
            public string Name { get; set; } = string.Empty;
            public string? ThemeName { get; set; }
            public string? ThemeBuiltInKey { get; set; }
            public CreditsProfileConfig? Config { get; set; }
        }

        private sealed class CreditsThemeExportDocument
        {
            public string Name { get; set; } = string.Empty;
            public string? Description { get; set; }
            public CreditsThemeConfig? Config { get; set; }
        }
    }
}
