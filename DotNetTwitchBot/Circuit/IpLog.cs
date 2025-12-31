using DotNetTwitchBot.Bot.Models.IpLogs;
using DotNetTwitchBot.Repository;
using System.Net;
using System.Net.Sockets;

namespace DotNetTwitchBot.Circuit
{
    public class IpLog(ILogger<IpLog> logger, IServiceScopeFactory scopeFactory)
    {
        public async Task AddLogEntry(string username, string userId, string ipAddress)
        {
            if (username.Equals("anonymous", StringComparison.OrdinalIgnoreCase)) return;
            await Task.WhenAll(
                AddOrUpdateIpEntry(username, userId, ipAddress),
                CheckForIPv6AndUpdateEntries(username, userId, ipAddress)
            );
        }

        public async Task CleanupOldIpLogs()
        {
            logger.LogInformation("Starting cleanup of old IP log entries.");
            await using var scope = scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            var cutoffDate = DateTime.Now.AddMonths(-6);
            var oldEntries = db.IpLogs.Find(x => x.ConnectedDate < cutoffDate);
            db.IpLogs.RemoveRange(oldEntries);
            var removedLogs = await db.SaveChangesAsync();
            logger.LogInformation("Cleanup complete. Removed {removedLogs} old IP log entries.", removedLogs);
        }

        private async Task AddOrUpdateIpEntry(string username, string userId, string ipAddress)
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            var existingEntry = await db.IpLogs.Find(x => x.Username.Equals(username) && x.Ip.Equals(ipAddress)).FirstOrDefaultAsync();
            if (existingEntry != null)
            {
                existingEntry.ConnectedDate = DateTime.Now;
                if(string.IsNullOrEmpty(existingEntry.UserId) && !string.IsNullOrEmpty(userId))
                {
                    existingEntry.UserId = userId;
                }
                existingEntry.Count++;
                db.IpLogs.Update(existingEntry);
            }
            else
            {
                await db.IpLogs.AddAsync(new IpLogEntry
                {
                    Username = username,
                    Ip = ipAddress,
                    UserId = userId
                });
            }
            await db.SaveChangesAsync();
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
