using PenguinTwitchBot.Database.Bot.Models.IpLogs;
using PenguinTwitchBot.Database.Repository;
using PenguinTwitchBot.Services;
using System.Net;
using System.Net.Sockets;

namespace PenguinTwitchBot.Circuit
{
    public class IpLog(ILogger<IpLog> logger, IServiceScopeFactory scopeFactory, IIpLogRetentionSettingsService ipLogRetentionSettingsService)
    {
        public async Task AddLogEntry(string username, string userId, string ipAddress)
        {
            if (string.IsNullOrWhiteSpace(userId) ||
                userId.Equals("anonymous", StringComparison.OrdinalIgnoreCase) ||
                string.IsNullOrWhiteSpace(username) ||
                username.Equals("anonymous", StringComparison.OrdinalIgnoreCase) ||
                string.IsNullOrWhiteSpace(ipAddress) ||
                ipAddress.Equals("unknown", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            try
            {
                var normalizedUsername = Database.Bot.Core.UsernameNormalizer.Normalize(username);
                await Task.WhenAll(
                    AddOrUpdateIpEntry(normalizedUsername, userId, ipAddress),
                    CheckForIPv6AndUpdateEntries(normalizedUsername, userId, ipAddress)
                );
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to log IP entry for user {Username} ({UserId}) at IP {IpAddress}", username, userId, ipAddress);
            }
        }

        public async Task CleanupOldIpLogs()
        {
            try
            {
                logger.LogInformation("Starting cleanup of old IP log entries.");
                await using var scope = scopeFactory.CreateAsyncScope();
                var db = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
                var monthsToKeep = await ipLogRetentionSettingsService.GetIpLogMonthsToKeepAsync(6);
                monthsToKeep = Math.Max(0, monthsToKeep);
                var cutoffDate = DateTime.UtcNow.AddMonths(-monthsToKeep);
                var removedLogs = await db.IpLogs.Find(x => x.ConnectedDate < cutoffDate).ExecuteDeleteAsync();
                logger.LogInformation("Cleanup complete. Removed {removedLogs} old IP log entries using retention of {monthsToKeep} months.", removedLogs, monthsToKeep);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to cleanup old IP log entries.");
            }
        }

        private async Task AddOrUpdateIpEntry(string username, string userId, string ipAddress)
        {
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var db = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
                var matchingEntries = await db.IpLogs.Find(x => x.UserId == userId && x.Ip == ipAddress).ToListAsync();
                if (matchingEntries.Count > 0)
                {
                    var existingEntry = matchingEntries[0];
                    existingEntry.ConnectedDate = DateTime.UtcNow;
                    existingEntry.Count++;
                    if (!string.Equals(existingEntry.Username, username, StringComparison.Ordinal))
                    {
                        existingEntry.Username = username;
                    }

                    if (matchingEntries.Count > 1)
                    {
                        for (int i = 1; i < matchingEntries.Count; i++)
                        {
                            existingEntry.Count += matchingEntries[i].Count;
                            db.IpLogs.Remove(matchingEntries[i]);
                        }
                    }

                    db.IpLogs.Update(existingEntry);
                }
                else
                {
                    await db.IpLogs.AddAsync(new IpLogEntry
                    {
                        Username = username,
                        Ip = ipAddress,
                        UserId = userId,
                        Count = 1,
                        ConnectedDate = DateTime.UtcNow
                    });
                }
                await db.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to add or update IP log entry for user {Username} ({UserId}) at IP {IpAddress}", username, userId, ipAddress);
            }
        }

        private async Task CheckForIPv6AndUpdateEntries(string username, string userId, string ipAddress)
        {
            if (IPAddress.TryParse(ipAddress, out IPAddress? address))
            {
                if (address.AddressFamily == AddressFamily.InterNetworkV6)
                {
                    // Check if it's an IPv4-mapped IPv6 address
                    if (address.IsIPv4MappedToIPv6)
                    {
                        var ipv4Address = address.MapToIPv4();
                        await AddOrUpdateIpEntry(username, userId, ipv4Address.ToString());
                    }
                    else
                    {
                        var prefixes = new List<string> { GetIPv6Prefixes(ipAddress, 48), GetIPv6Prefixes(ipAddress, 64) };
                        var tasks = new List<Task>();
                        foreach (var prefix in prefixes)
                        {
                            if (!string.IsNullOrEmpty(prefix))
                                tasks.Add(AddOrUpdateIpEntry(username, userId, prefix));
                        }
                        await Task.WhenAll(tasks);
                    }
                }
            }
        }

        private string GetIPv6Prefixes(string ipv6Address, int prefixLength)
        {
            if (prefixLength < 0 || prefixLength > 128)
            {
                throw new ArgumentOutOfRangeException(nameof(prefixLength), "Prefix length must be between 0 and 128.");
            }

            string prefix = "";

            if (IPAddress.TryParse(ipv6Address, out IPAddress? ipAddress))
            {
                if (ipAddress.AddressFamily == AddressFamily.InterNetworkV6)
                {
                    byte[] addressBytes = ipAddress.GetAddressBytes();
                    byte[] prefixBytes = new byte[16]; // IPv6 addresses are 16 bytes

                    // Copy the prefix portion
                    int bytesToCopy = prefixLength / 8;
                    Array.Copy(addressBytes, 0, prefixBytes, 0, bytesToCopy);

                    // Handle partial last byte if prefixLength is not a multiple of 8
                    if (prefixLength % 8 != 0)
                    {
                        int lastByteIndex = bytesToCopy;
                        byte mask = (byte)(0xFF << (8 - (prefixLength % 8)));
                        prefixBytes[lastByteIndex] = (byte)(addressBytes[lastByteIndex] & mask);
                    }

                    // Create a new IPAddress from the prefix bytes and add it to the set
                    // This creates a "network address" representation of the prefix
                    IPAddress prefixAddress = new(prefixBytes);
                    prefix = $"{prefixAddress}/{prefixLength}";
                }
            }
            else
            {
                logger.LogWarning("Warning: Invalid IPv6 address format: {addressString}", ipv6Address);
            }
            
            return prefix;
        }
    }
}
