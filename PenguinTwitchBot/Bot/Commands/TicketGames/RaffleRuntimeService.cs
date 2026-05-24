using System.Collections.Concurrent;
using System.Security.Cryptography;
using PenguinTwitchBot.Bot.Core.Points;

namespace PenguinTwitchBot.Bot.Commands.TicketGames
{
    public interface IRaffleRuntimeService
    {
        Task<RaffleOperationResult> StartAsync(RaffleStartRequest request);
        Task<RaffleOperationResult> EnterAsync(string raffleKey, string username);
        Task<RaffleOperationResult> EndAsync(string raffleKey);
        Task<RaffleOperationResult> SetWinnerCountAsync(string raffleKey, int winnerCount);
        Task<RaffleOperationResult> SetTotalAwardAsync(string raffleKey, long totalAward);
        Task<RaffleOperationResult> GetEntryCountAsync(string raffleKey);
    }

    public sealed class RaffleRuntimeService(IPointsSystem pointsSystem) : IRaffleRuntimeService
    {
        private readonly ConcurrentDictionary<string, ActiveRaffle> _raffles = new(StringComparer.OrdinalIgnoreCase);
        private readonly SemaphoreSlim _lock = new(1, 1);

        public async Task<RaffleOperationResult> StartAsync(RaffleStartRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.RaffleKey))
            {
                return RaffleOperationResult.NotFound(string.Empty);
            }

            var raffleKey = request.RaffleKey.Trim();
            var raffleName = string.IsNullOrWhiteSpace(request.RaffleName) ? raffleKey : request.RaffleName.Trim();
            var pointGameName = string.IsNullOrWhiteSpace(request.PointGameName) ? "raffle" : request.PointGameName.Trim();
            var joinCommand = request.JoinCommand?.Trim() ?? string.Empty;
            var winnerCount = Math.Max(1, request.WinnerCount);
            var totalAward = Math.Max(0, request.TotalAward);

            await pointsSystem.RegisterDefaultPointForGame(pointGameName);
            var pointType = await pointsSystem.GetPointTypeForGame(pointGameName);
            var pointTypeName = string.IsNullOrWhiteSpace(pointType.Name) ? pointGameName : pointType.Name;

            var raffle = new ActiveRaffle(
                raffleKey,
                raffleName,
                joinCommand,
                pointGameName,
                pointTypeName,
                winnerCount,
                totalAward);

            await _lock.WaitAsync();
            try
            {
                if (_raffles.ContainsKey(raffleKey))
                {
                    return CreateResult(_raffles[raffleKey], "already_running", success: false);
                }

                _raffles[raffleKey] = raffle;
                return CreateResult(raffle, "started");
            }
            finally
            {
                _lock.Release();
            }
        }

        public async Task<RaffleOperationResult> EnterAsync(string raffleKey, string username)
        {
            if (string.IsNullOrWhiteSpace(raffleKey) || string.IsNullOrWhiteSpace(username))
            {
                return RaffleOperationResult.NotFound(raffleKey);
            }

            await _lock.WaitAsync();
            try
            {
                if (!_raffles.TryGetValue(raffleKey.Trim(), out var raffle))
                {
                    return RaffleOperationResult.NotRunning(raffleKey.Trim());
                }

                if (raffle.IsEnding)
                {
                    return CreateResult(raffle, "ending_in_progress", success: false);
                }

                if (!raffle.Entries.Add(username.Trim()))
                {
                    return CreateResult(raffle, "already_entered", success: false, username: username.Trim(), alreadyEntered: true);
                }

                return CreateResult(raffle, "entered", username: username.Trim(), joined: true);
            }
            finally
            {
                _lock.Release();
            }
        }

        public async Task<RaffleOperationResult> EndAsync(string raffleKey)
        {
            if (string.IsNullOrWhiteSpace(raffleKey))
            {
                return RaffleOperationResult.NotFound(string.Empty);
            }

            ActiveRaffle raffle;
            List<string> winners;
            long eachAward;
            var trimmedKey = raffleKey.Trim();

            await _lock.WaitAsync();
            try
            {
                if (!_raffles.TryGetValue(trimmedKey, out var raffleValue) || raffleValue is null)
                {
                    return RaffleOperationResult.NotRunning(trimmedKey);
                }

                raffle = raffleValue;

                if (raffle.IsEnding)
                {
                    return CreateResult(raffle, "ending_in_progress", success: false);
                }

                if (raffle.Entries.Count == 0)
                {
                    _raffles.TryRemove(trimmedKey, out _);
                    return CreateResult(raffle, "no_entries", success: false, isActive: false);
                }

                raffle.IsEnding = true;
                if (raffle.PendingWinners.Count == 0)
                {
                    winners = PickWinners(raffle.Entries, raffle.WinnerCount);
                    var resolvedWinnerCount = Math.Max(1, winners.Count);
                    eachAward = resolvedWinnerCount == 0
                        ? 0
                        : (long)Math.Ceiling((double)raffle.TotalAward / resolvedWinnerCount);

                    raffle.PendingWinners = winners;
                    raffle.PendingEachAward = eachAward;
                }

                winners = [.. raffle.PendingWinners];
                eachAward = raffle.PendingEachAward;
            }
            finally
            {
                _lock.Release();
            }

            try
            {
                foreach (var winner in winners)
                {
                    var shouldAward = false;

                    await _lock.WaitAsync();
                    try
                    {
                        shouldAward = !raffle.AwardedWinners.Contains(winner);
                    }
                    finally
                    {
                        _lock.Release();
                    }

                    if (!shouldAward || eachAward <= 0)
                    {
                        continue;
                    }

                    await pointsSystem.AddPointsByUsernameAndGame(winner, raffle.PointGameName, eachAward);

                    await _lock.WaitAsync();
                    try
                    {
                        raffle.AwardedWinners.Add(winner);
                    }
                    finally
                    {
                        _lock.Release();
                    }
                }
            }
            catch
            {
                await _lock.WaitAsync();
                try
                {
                    raffle.IsEnding = false;
                }
                finally
                {
                    _lock.Release();
                }

                return CreateResult(
                    raffle,
                    "payout_failed",
                    success: false,
                    isActive: true,
                    winners: raffle.PendingWinners,
                    eachAward: eachAward,
                    awardedTotal: eachAward * raffle.AwardedWinners.Count,
                    resolvedWinnerCount: raffle.PendingWinners.Count);
            }

            await _lock.WaitAsync();
            try
            {
                _raffles.TryRemove(trimmedKey, out _);
            }
            finally
            {
                _lock.Release();
            }

            return CreateResult(
                raffle,
                "ended",
                isActive: false,
                winners: raffle.PendingWinners,
                eachAward: eachAward,
                awardedTotal: eachAward * raffle.AwardedWinners.Count,
                resolvedWinnerCount: raffle.PendingWinners.Count);
        }

        public async Task<RaffleOperationResult> SetWinnerCountAsync(string raffleKey, int winnerCount)
        {
            if (string.IsNullOrWhiteSpace(raffleKey))
            {
                return RaffleOperationResult.NotFound(string.Empty);
            }

            await _lock.WaitAsync();
            try
            {
                if (!_raffles.TryGetValue(raffleKey.Trim(), out var raffle))
                {
                    return RaffleOperationResult.NotRunning(raffleKey.Trim());
                }

                if (raffle.IsEnding)
                {
                    return CreateResult(raffle, "ending_in_progress", success: false);
                }

                raffle.WinnerCount = Math.Max(1, winnerCount);
                return CreateResult(raffle, "winner_count_updated");
            }
            finally
            {
                _lock.Release();
            }
        }

        public async Task<RaffleOperationResult> SetTotalAwardAsync(string raffleKey, long totalAward)
        {
            if (string.IsNullOrWhiteSpace(raffleKey))
            {
                return RaffleOperationResult.NotFound(string.Empty);
            }

            await _lock.WaitAsync();
            try
            {
                if (!_raffles.TryGetValue(raffleKey.Trim(), out var raffle))
                {
                    return RaffleOperationResult.NotRunning(raffleKey.Trim());
                }

                if (raffle.IsEnding)
                {
                    return CreateResult(raffle, "ending_in_progress", success: false);
                }

                raffle.TotalAward = Math.Max(0, totalAward);
                return CreateResult(raffle, "total_award_updated");
            }
            finally
            {
                _lock.Release();
            }
        }

        public async Task<RaffleOperationResult> GetEntryCountAsync(string raffleKey)
        {
            if (string.IsNullOrWhiteSpace(raffleKey))
            {
                return RaffleOperationResult.NotFound(string.Empty);
            }

            await _lock.WaitAsync();
            try
            {
                if (!_raffles.TryGetValue(raffleKey.Trim(), out var raffle))
                {
                    return RaffleOperationResult.NotRunning(raffleKey.Trim());
                }

                return CreateResult(raffle, "entry_count");
            }
            finally
            {
                _lock.Release();
            }
        }

        private static List<string> PickWinners(HashSet<string> entries, int configuredWinnerCount)
        {
            var pool = entries.ToList();
            var winnersToPick = Math.Min(pool.Count, Math.Max(1, configuredWinnerCount));
            var winners = new List<string>(winnersToPick);

            for (var index = 0; index < winnersToPick; index++)
            {
                var winnerIndex = RandomNumberGenerator.GetInt32(0, pool.Count);
                winners.Add(pool[winnerIndex]);
                pool.RemoveAt(winnerIndex);
            }

            return winners;
        }

        private static RaffleOperationResult CreateResult(
            ActiveRaffle raffle,
            string status,
            bool success = true,
            bool? isActive = null,
            string username = "",
            bool joined = false,
            bool alreadyEntered = false,
            IReadOnlyList<string>? winners = null,
            long eachAward = 0,
            long awardedTotal = 0,
            int? resolvedWinnerCount = null)
        {
            return new RaffleOperationResult
            {
                Success = success,
                Status = status,
                RaffleKey = raffle.RaffleKey,
                RaffleName = raffle.RaffleName,
                JoinCommand = raffle.JoinCommand,
                PointGameName = raffle.PointGameName,
                PointTypeName = raffle.PointTypeName,
                IsActive = isActive ?? true,
                EntryCount = raffle.Entries.Count,
                WinnerCount = raffle.WinnerCount,
                TotalAward = raffle.TotalAward,
                Username = username,
                Joined = joined,
                AlreadyEntered = alreadyEntered,
                Winners = winners ?? [],
                EachAward = eachAward,
                AwardedTotal = awardedTotal,
                ResolvedWinnerCount = resolvedWinnerCount ?? raffle.WinnerCount
            };
        }

        private sealed class ActiveRaffle(
            string raffleKey,
            string raffleName,
            string joinCommand,
            string pointGameName,
            string pointTypeName,
            int winnerCount,
            long totalAward)
        {
            public string RaffleKey { get; } = raffleKey;
            public string RaffleName { get; } = raffleName;
            public string JoinCommand { get; } = joinCommand;
            public string PointGameName { get; } = pointGameName;
            public string PointTypeName { get; } = pointTypeName;
            public int WinnerCount { get; set; } = winnerCount;
            public long TotalAward { get; set; } = totalAward;
            public HashSet<string> Entries { get; } = new(StringComparer.OrdinalIgnoreCase);
            public bool IsEnding { get; set; }
            public List<string> PendingWinners { get; set; } = [];
            public long PendingEachAward { get; set; }
            public HashSet<string> AwardedWinners { get; } = new(StringComparer.OrdinalIgnoreCase);
        }
    }

    public sealed class RaffleStartRequest
    {
        public string RaffleKey { get; init; } = string.Empty;
        public string RaffleName { get; init; } = string.Empty;
        public string JoinCommand { get; init; } = string.Empty;
        public string PointGameName { get; init; } = "raffle";
        public int WinnerCount { get; init; } = 1;
        public long TotalAward { get; init; }
    }

    public sealed class RaffleOperationResult
    {
        public bool Success { get; init; }
        public string Status { get; init; } = string.Empty;
        public string RaffleKey { get; init; } = string.Empty;
        public string RaffleName { get; init; } = string.Empty;
        public string JoinCommand { get; init; } = string.Empty;
        public string PointGameName { get; init; } = string.Empty;
        public string PointTypeName { get; init; } = string.Empty;
        public bool IsActive { get; init; }
        public int EntryCount { get; init; }
        public int WinnerCount { get; init; }
        public long TotalAward { get; init; }
        public string Username { get; init; } = string.Empty;
        public bool Joined { get; init; }
        public bool AlreadyEntered { get; init; }
        public IReadOnlyList<string> Winners { get; init; } = [];
        public long EachAward { get; init; }
        public long AwardedTotal { get; init; }
        public int ResolvedWinnerCount { get; init; }

        public static RaffleOperationResult NotFound(string raffleKey) => new()
        {
            Success = false,
            Status = "invalid_request",
            RaffleKey = raffleKey,
            IsActive = false
        };

        public static RaffleOperationResult NotRunning(string raffleKey) => new()
        {
            Success = false,
            Status = "not_running",
            RaffleKey = raffleKey,
            IsActive = false
        };
    }
}