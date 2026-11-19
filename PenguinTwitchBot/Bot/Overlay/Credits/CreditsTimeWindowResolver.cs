using Microsoft.EntityFrameworkCore;
using PenguinTwitchBot.Database.Bot.Models.Credits;
using PenguinTwitchBot.Database.Repository;

namespace PenguinTwitchBot.Bot.Overlay.Credits
{
    public sealed record ResolvedCreditsWindow(
        DateTime StartUtc,
        DateTime EndUtc,
        CreditStreamSession? Session);

    public sealed class CreditsTimeWindowResolver(TimeProvider? timeProvider = null)
    {
        private readonly TimeProvider _timeProvider = timeProvider ?? TimeProvider.System;

        public async Task<ResolvedCreditsWindow> ResolveAsync(
            IUnitOfWork db,
            CreditsTimeWindowConfig config,
            string? overrideMode = null,
            int? overrideLookbackMinutes = null,
            CancellationToken cancellationToken = default)
        {
            var nowUtc = _timeProvider.GetUtcNow().UtcDateTime;
            var mode = string.IsNullOrWhiteSpace(overrideMode) ? config.Mode : overrideMode.Trim();
            var grace = TimeSpan.FromMinutes(Math.Max(0, config.PreStreamGraceMinutes));

            switch (mode)
            {
                case CreditTimeWindowModes.LastCompletedStream:
                {
                    var lastCompleted = await db.CreditStreamSessions
                        .Find(s => !s.IsManualCheckpoint && s.EndedAtUtc != null)
                        .OrderByDescending(s => s.EndedAtUtc)
                        .FirstOrDefaultAsync(cancellationToken);

                    if (lastCompleted != null)
                    {
                        var start = lastCompleted.StartedAtUtc - grace;
                        var end = lastCompleted.EndedAtUtc!.Value;
                        if (end < start) end = start;
                        return new ResolvedCreditsWindow(start, end, lastCompleted);
                    }

                    // Fallback to most recent session or rolling 6 hours
                    var anySession = await db.CreditStreamSessions
                        .Find(s => !s.IsManualCheckpoint)
                        .OrderByDescending(s => s.StartedAtUtc)
                        .FirstOrDefaultAsync(cancellationToken);

                    if (anySession != null)
                    {
                        return CreateOrderedWindow(
                            anySession.StartedAtUtc - grace,
                            anySession.EndedAtUtc ?? nowUtc,
                            anySession);
                    }

                    return CreateOrderedWindow(nowUtc.AddHours(-6), nowUtc, null);
                }

                case CreditTimeWindowModes.RollingLookback:
                {
                    TimeSpan span;
                    if (overrideLookbackMinutes is > 0)
                    {
                        span = TimeSpan.FromMinutes(overrideLookbackMinutes.Value);
                    }
                    else
                    {
                        var val = Math.Max(1, config.LookbackValue);
                        span = config.LookbackUnit switch
                        {
                            CreditLookbackUnits.Minutes => TimeSpan.FromMinutes(val),
                            CreditLookbackUnits.Days => TimeSpan.FromDays(val),
                            _ => TimeSpan.FromHours(val)
                        };
                    }

                    var lookbackStart = span >= nowUtc - DateTime.MinValue ? DateTime.MinValue : nowUtc - span;
                    return CreateOrderedWindow(lookbackStart, nowUtc, null);
                }

                case CreditTimeWindowModes.SinceManualCheckpoint:
                {
                    var latestCheckpoint = await db.CreditStreamSessions
                        .Find(s => s.IsManualCheckpoint)
                        .OrderByDescending(s => s.StartedAtUtc)
                        .FirstOrDefaultAsync(cancellationToken);

                    var startUtc = config.ManualCheckpointUtc ?? latestCheckpoint?.StartedAtUtc;
                    if (config.ManualCheckpointUtc.HasValue && latestCheckpoint != null)
                    {
                        startUtc = config.ManualCheckpointUtc.Value > latestCheckpoint.StartedAtUtc
                            ? config.ManualCheckpointUtc.Value
                            : latestCheckpoint.StartedAtUtc;
                    }

                    return CreateOrderedWindow(startUtc ?? nowUtc.AddHours(-6), nowUtc, latestCheckpoint);
                }

                case CreditTimeWindowModes.CustomDateRange:
                {
                    var start = config.CustomStartUtc.HasValue
                        ? DateTime.SpecifyKind(config.CustomStartUtc.Value, DateTimeKind.Utc)
                        : nowUtc.AddHours(-6);
                    var end = config.CustomEndUtc.HasValue
                        ? DateTime.SpecifyKind(config.CustomEndUtc.Value, DateTimeKind.Utc)
                        : nowUtc;
                    if (end < start) end = start;
                    return new ResolvedCreditsWindow(start, end, null);
                }

                case CreditTimeWindowModes.SpecificSession:
                {
                    if (config.SpecificSessionId.HasValue)
                    {
                        var session = await db.CreditStreamSessions.GetByIdAsync(config.SpecificSessionId.Value);
                        if (session != null)
                        {
                            var start = session.IsManualCheckpoint ? session.StartedAtUtc : session.StartedAtUtc - grace;
                            var end = session.EndedAtUtc ?? nowUtc;
                            if (end < start) end = start;
                            return new ResolvedCreditsWindow(start, end, session);
                        }
                    }

                    return await ResolveSinceStreamStartAsync(db, grace, nowUtc, cancellationToken);
                }

                case CreditTimeWindowModes.SinceStreamStart:
                default:
                    return await ResolveSinceStreamStartAsync(db, grace, nowUtc, cancellationToken);
            }
        }

        private static async Task<ResolvedCreditsWindow> ResolveSinceStreamStartAsync(
            IUnitOfWork db,
            TimeSpan grace,
            DateTime nowUtc,
            CancellationToken cancellationToken)
        {
            var openSession = await db.CreditStreamSessions
                .Find(s => !s.IsManualCheckpoint && s.EndedAtUtc == null)
                .OrderByDescending(s => s.StartedAtUtc)
                .FirstOrDefaultAsync(cancellationToken);

            if (openSession != null)
            {
                return CreateOrderedWindow(openSession.StartedAtUtc - grace, nowUtc, openSession);
            }

            // Offline fallback: use the most recent stream session so streamers can run or preview credits right after ending stream
            var lastSession = await db.CreditStreamSessions
                .Find(s => !s.IsManualCheckpoint)
                .OrderByDescending(s => s.StartedAtUtc)
                .FirstOrDefaultAsync(cancellationToken);

            if (lastSession != null)
            {
                return CreateOrderedWindow(lastSession.StartedAtUtc - grace, lastSession.EndedAtUtc ?? nowUtc, lastSession);
            }

            return CreateOrderedWindow(nowUtc.AddHours(-6), nowUtc, null);
        }

        private static ResolvedCreditsWindow CreateOrderedWindow(
            DateTime startUtc,
            DateTime endUtc,
            CreditStreamSession? session)
        {
            if (endUtc < startUtc)
                endUtc = startUtc;
            return new ResolvedCreditsWindow(startUtc, endUtc, session);
        }
    }
}
