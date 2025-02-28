using DotNetTwitchBot.Bot.Models.IpLogs;
using DotNetTwitchBot.Repository;

namespace DotNetTwitchBot.Circuit
{
    public class IpLog(IServiceScopeFactory scopeFactory)
    {
        public async Task AddLogEntry(string username, string ipAddress)
        {
            if (username.Equals("anonymous", StringComparison.OrdinalIgnoreCase)) return;

            await using var scope = scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            var existingEntry = await db.IpLogs.Find(x => x.Username.Equals(username) && x.Ip.Equals(ipAddress)).FirstOrDefaultAsync();
            if (existingEntry != null)
            {
                existingEntry.ConnectedDate = DateTime.Now;
                existingEntry.Count++;
                db.IpLogs.Update(existingEntry);
            }
            else
            {
                await db.IpLogs.AddAsync(new IpLogEntry
                {
                    Username = username,
                    Ip = ipAddress
                });
            }
            await db.SaveChangesAsync();
        }
    }
}
